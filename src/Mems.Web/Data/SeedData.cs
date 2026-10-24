/*
 * Project: Municipal Elections Management System (MEMS)
 * Course: COMP4976
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Initializes sample election data, administrator roles, and approved demo accounts.
 */
using Mems.Web.Models;
using Mems.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Mems.Web.Data;

/// <summary>Applies migrations and repeatable setup without refreshing customized candidate content.</summary>
public static class SeedData
{
    /// <summary>Initializes domain data and role memberships; production user creation requires explicit configuration.</summary>
    public static async Task Initialize(IServiceProvider services, bool development, IConfiguration config)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        // WAL shared-memory files are unsuitable for App Service network-mounted storage.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=DELETE;");
        if (!await db.Municipalities.AnyAsync())
        {
            foreach (var name in new[] { "Pitt Meadows", "Maple Ridge", "Burnaby", "Surrey" })
            {
                var municipality = new Municipality { Name = name, Description = $"Explore the people standing for local office in {name}. Sample election data for this student application." };
                foreach (var (positionName, seats) in new[] { ("Mayor", 1), ("Councilor", 6), ("School Trustee", 2) })
                    municipality.Positions.Add(new Position { Name = positionName, SeatsAvailable = seats });
                db.Add(municipality);
            }
            await db.SaveChangesAsync();
            var positions = await db.Positions.Include(x => x.Municipality).ToListAsync();
            foreach (var sample in DemoCandidates.All)
                db.Candidates.Add(DemoCandidates.Create(sample, positions.Single(x => x.Name == sample.Position && x.Municipality.Name == sample.Municipality).PositionId));
            await db.SaveChangesAsync();
        }
        // Only exact legacy samples are upgraded; claimed samples and customized rows are left unchanged.
        await DemoCandidates.UpgradeLegacy(db);
        var roles = services.GetRequiredService<RoleManager<ApplicationRole>>();
        await EnsureRole(roles, AdminScope.SuperRole);
        await EnsureRole(roles, AdminScope.MunicipalRole);
        // Role-to-municipality IDs remain authoritative even if a municipality is later renamed.
        foreach (var municipality in await db.Municipalities.ToListAsync())
            if (!await db.Roles.AnyAsync(x => x.MunicipalityId == municipality.MunicipalityId))
                Check(await roles.CreateAsync(new ApplicationRole { Name = municipality.Name + " Admin", MunicipalityId = municipality.MunicipalityId }));

        // Never seed the assignment's known password outside Development.
        if (!development && !config.GetValue<bool>("Seed:CreateUsers")) return;
        var password = development ? "P@$$w0rd" : config["Seed:Password"];
        if (string.IsNullOrWhiteSpace(password) || (!development && password == "P@$$w0rd"))
            throw new InvalidOperationException("Set a unique Seed__Password before enabling production administrator creation.");
        var users = services.GetRequiredService<UserManager<IdentityUser>>();
        foreach (var username in new[] { "super", "pm" })
        {
            var user = await users.FindByNameAsync(username);
            if (user is null)
            {
                user = new IdentityUser { UserName = username };
                Check(await users.CreateAsync(user, password));
            }
            var assigned = username == "super" ? new[] { AdminScope.SuperRole } : new[] { AdminScope.MunicipalRole, "Pitt Meadows Admin" };
            foreach (var role in assigned)
                if (!await users.IsInRoleAsync(user, role)) Check(await users.AddToRoleAsync(user, role));
        }
    }
    private static async Task EnsureRole(RoleManager<ApplicationRole> roles, string name)
    {
        if (!await roles.RoleExistsAsync(name)) Check(await roles.CreateAsync(new ApplicationRole { Name = name }));
    }
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
