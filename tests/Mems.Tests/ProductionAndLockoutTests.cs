using System.Net;
using Mems.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mems.Tests;

public class ProductionFactory(bool createUsers, string? password = null) : MemsFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment("Production");
        builder.UseSetting("Seed:CreateUsers", createUsers.ToString());
        if (password is not null) builder.UseSetting("Seed:Password", password);
    }
}

public class ProductionAndLockoutTests
{
    [Fact]
    public async Task Production_does_not_create_demo_users_by_default()
    {
        await using var app = new ProductionFactory(false);
        using var browser = app.Browser();
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/")).StatusCode);
        Assert.Equal(0, await app.Query(db => db.Users.CountAsync()));
        Assert.True(await app.Query(db => db.Municipalities.CountAsync()) > 0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("P@$$w0rd")]
    public async Task Production_refuses_missing_or_known_demo_password(string? password)
    {
        await using var app = new ProductionFactory(true, password);
        var ex = Assert.Throws<InvalidOperationException>(() => app.Browser());
        Assert.Contains("unique Seed__Password", ex.Message);
    }

    [Fact]
    public async Task Identity_lockout_is_enforced_after_five_failed_attempts()
    {
        await using var app = new MemsFactory();
        using var browser = app.Browser();
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser>>();
        var pm = await users.FindByNameAsync("pm");
        Assert.NotNull(pm);
        // Exercise the real sign-in service and persisted Identity lockout, independently of the HTTP rate limiter.
        var signIn = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.SignInManager<Microsoft.AspNetCore.Identity.IdentityUser>>();
        for (var i = 0; i < 5; i++)
            await signIn.CheckPasswordSignInAsync(pm, "incorrect", lockoutOnFailure: true);
        Assert.True(await users.IsLockedOutAsync(pm));
        Assert.True((await signIn.CheckPasswordSignInAsync(pm, "P@$$w0rd", lockoutOnFailure: true)).IsLockedOut);
    }
}
