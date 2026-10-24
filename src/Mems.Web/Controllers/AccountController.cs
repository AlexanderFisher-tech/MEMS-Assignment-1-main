/*
 * Project: Municipal Elections Management System (MEMS)
 * Course: COMP4976
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Handles administrator sign-in, sign-out, and access-denied responses.
 */
using Mems.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mems.Web.Controllers;

/// <summary>Uses Identity cookie authentication with lockout and local-only return redirects.</summary>
public class AccountController(SignInManager<IdentityUser> signIn, UserManager<IdentityUser> users) : Controller
{
    [HttpGet, Authorize(Policy = "Admin"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult ChangePassword() => View(new ChangePasswordForm());

    /// <summary>Changes only the signed-in administrator's password and refreshes their cookie.</summary>
    [HttpPost, Authorize(Policy = "Admin"), ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ChangePassword(ChangePasswordForm form)
    {
        if (ModelState.IsValid)
        {
            // Resolve the owner from the cookie; never accept an account ID from the form.
            var user = await users.GetUserAsync(User);
            if (user is null) return Challenge();
            var result = await users.ChangePasswordAsync(user, form.CurrentPassword, form.NewPassword);
            if (result.Succeeded)
            {
                await signIn.RefreshSignInAsync(user);
                TempData["Notice"] = "Your password has been changed successfully.";
                return RedirectToAction(nameof(ChangePassword));
            }
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
        }
        // Keep validation errors without redisplaying submitted passwords.
        foreach (var entry in ModelState.Values) { entry.RawValue = null; entry.AttemptedValue = null; }
        return View(new ChangePasswordForm());
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl) => View(new LoginForm { ReturnUrl = returnUrl });
    [HttpPost, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginForm form)
    {
        if (!ModelState.IsValid) return View(form);
        var result = await signIn.PasswordSignInAsync(form.UserName, form.Password, form.RememberMe, lockoutOnFailure: true);
        // A supplied return URL must be local to avoid redirecting a successful login to another site.
        if (result.Succeeded) return Url.IsLocalUrl(form.ReturnUrl) ? LocalRedirect(form.ReturnUrl!) : RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        ModelState.AddModelError("", result.IsLockedOut ? "Too many attempts. Please try again in 15 minutes." : "Invalid username or password.");
        return View(form);
    }
    [HttpPost, Authorize]
    public async Task<IActionResult> Logout() { await signIn.SignOutAsync(); return RedirectToAction("Index", "Municipalities"); }
    public IActionResult AccessDenied() { Response.StatusCode = 403; return View(); }
}
