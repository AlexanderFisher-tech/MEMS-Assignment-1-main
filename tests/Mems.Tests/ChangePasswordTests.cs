using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mems.Tests;

public class ChangePasswordTests
{
    private const string Page = "/Account/ChangePassword";
    private static string Password() => Guid.NewGuid().ToString("N") + "aA1!";

    private static async Task<HttpResponseMessage> Post(HttpClient browser, string path, Dictionary<string, string> fields)
    {
        var html = await browser.GetStringAsync(path);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
        return await browser.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    [Theory]
    [InlineData("super")]
    [InlineData("pm")]
    public async Task Administrator_changes_only_own_password_and_can_sign_in_again(string username)
    {
        var original = Password();
        var replacement = Password();
        await using var app = new ProductionFactory(true, original);
        using var browser = app.Browser();
        var login = await Post(browser, "/Account/Login", new() { ["UserName"] = username, ["Password"] = original });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains(Page, await browser.GetStringAsync("/Admin"));
        var before = await app.Query(async db => new {
            Users = await db.Users.CountAsync(), Roles = await db.UserRoles.CountAsync(),
            Candidates = await db.Candidates.CountAsync(), Positions = await db.Positions.CountAsync(),
            Municipalities = await db.Municipalities.CountAsync()
        });
        var changed = await Post(browser, Page, new() {
            ["CurrentPassword"] = original, ["NewPassword"] = replacement, ["ConfirmNewPassword"] = replacement,
            ["UserName"] = username == "super" ? "pm" : "super"
        });
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        Assert.Equal(Page, changed.Headers.Location!.ToString());
        Assert.Contains("Your password has been changed successfully.", await browser.GetStringAsync(Page));
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Admin")).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var owner = (await users.FindByNameAsync(username))!;
            Assert.True(await users.CheckPasswordAsync(owner, replacement));
            Assert.False(await users.CheckPasswordAsync(owner, original));
            var other = (await users.FindByNameAsync(username == "super" ? "pm" : "super"))!;
            Assert.True(await users.CheckPasswordAsync(other, original));
        }
        var after = await app.Query(async db => new {
            Users = await db.Users.CountAsync(), Roles = await db.UserRoles.CountAsync(),
            Candidates = await db.Candidates.CountAsync(), Positions = await db.Positions.CountAsync(),
            Municipalities = await db.Municipalities.CountAsync()
        });
        Assert.Equal(before, after);
        using var fresh = app.Browser();
        var oldLogin = await Post(fresh, "/Account/Login", new() { ["UserName"] = username, ["Password"] = original });
        Assert.Contains("Invalid username or password", await oldLogin.Content.ReadAsStringAsync());
        var newLogin = await Post(fresh, "/Account/Login", new() { ["UserName"] = username, ["Password"] = replacement });
        Assert.Equal(HttpStatusCode.Redirect, newLogin.StatusCode);
    }

    [Fact]
    public async Task Invalid_changes_preserve_password_and_do_not_echo_submitted_secrets()
    {
        var original = Password();
        var replacement = Password();
        await using var app = new ProductionFactory(true, original);
        using var browser = app.Browser();
        await Post(browser, "/Account/Login", new() { ["UserName"] = "pm", ["Password"] = original });
        foreach (var fields in new Dictionary<string, string>[] {
            new(),
            new() { ["CurrentPassword"] = Password(), ["NewPassword"] = replacement, ["ConfirmNewPassword"] = replacement },
            new() { ["CurrentPassword"] = original, ["NewPassword"] = replacement, ["ConfirmNewPassword"] = Password() },
            new() { ["CurrentPassword"] = original, ["NewPassword"] = "x", ["ConfirmNewPassword"] = "x" }
        })
        {
            var response = await Post(browser, Page, fields);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("validation-summary-errors", html);
            Assert.DoesNotContain(original, html);
            Assert.DoesNotContain(replacement, html);
            using var scope = app.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.True(await users.CheckPasswordAsync((await users.FindByNameAsync("pm"))!, original));
        }
    }

    [Fact]
    public async Task Password_change_requires_admin_authentication_and_antiforgery()
    {
        var original = Password();
        await using var app = new ProductionFactory(true, original);
        using var browser = app.Browser();
        var anonymous = await browser.GetAsync(Page);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.Contains("/Account/Login", anonymous.Headers.Location!.ToString());
        var anonymousPost = await browser.PostAsync(Page, new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, anonymousPost.StatusCode);
        await Post(browser, "/Account/Login", new() { ["UserName"] = "super", ["Password"] = original });
        var page = await browser.GetAsync(Page);
        Assert.True(page.Headers.CacheControl!.NoStore);
        var missingToken = await browser.PostAsync(Page, new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.True((await users.CreateAsync(new IdentityUser("nonadmin"), original)).Succeeded);
        }
        using var nonadmin = app.Browser();
        await Post(nonadmin, "/Account/Login", new() { ["UserName"] = "nonadmin", ["Password"] = original });
        var forbidden = await nonadmin.GetAsync(Page);
        Assert.Equal(HttpStatusCode.Redirect, forbidden.StatusCode);
        Assert.Contains("AccessDenied", forbidden.Headers.Location!.ToString());
    }
}
