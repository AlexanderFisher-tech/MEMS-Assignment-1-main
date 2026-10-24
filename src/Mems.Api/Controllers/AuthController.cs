/*
 * Project: Municipal Elections Companion API (MEMS)
 * Course: COMP4977
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Registers mobile users and exchanges valid credentials for JWT bearer tokens.
 */
using Mems.Api.Models;
using Mems.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Mems.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(UserManager<ApplicationUser> users, TokenService tokens) : ControllerBase
{
    /// <summary>Creates an account. Passwords need at least 6 characters.</summary>
    [HttpPost("register")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MessageResponse>> Register(RegisterRequest request)
    {
        var email = request.Email!.Trim();
        if (await users.FindByEmailAsync(email) is not null)
            return BadRequest(new MessageResponse("An account with this email already exists."));
        var user = new ApplicationUser { Name = request.Name!.Trim(), Email = email, UserName = email };
        var result = await users.CreateAsync(user, request.Password!);
        if (!result.Succeeded)
            return BadRequest(new MessageResponse(string.Join(" ", result.Errors.Select(x => x.Description))));
        return Ok(new MessageResponse("Registration successful"));
    }

    /// <summary>Returns a JWT to send as "Authorization: Bearer {token}" on protected endpoints.</summary>
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email!.Trim());
        if (user is null || !await users.CheckPasswordAsync(user, request.Password!))
            return Unauthorized(new MessageResponse("Invalid email or password"));
        return Ok(new LoginResponse(tokens.CreateToken(user), user.Name, user.Email!));
    }
}
