/*
 * Project: Municipal Elections Companion API (MEMS)
 * Course: COMP4977
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Applies migrations and seeds sample election data on startup so deployments need no manual database steps.
 */
using Mems.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Mems.Api.Data;

public static class SeedData
{
    private static readonly (string Name, string Logo, string Description)[] Municipalities = [
        ("Pitt Meadows", "pitt-meadows", "A rural-urban community on the Fraser and Pitt rivers known for farmland, dikes, and trails. Sample election data for a student application."),
        ("Maple Ridge", "maple-ridge", "A growing city between the Fraser River and the Golden Ears mountains with a strong focus on parks and neighbourhood services. Sample election data for a student application."),
        ("Burnaby", "burnaby", "A large, diverse city at the centre of Metro Vancouver with busy town centres, parks, and post-secondary campuses. Sample election data for a student application."),
        ("Surrey", "surrey", "One of British Columbia's fastest-growing cities, with many distinct town centres and neighbourhoods. Sample election data for a student application."),
    ];

    private static readonly (string Name, int Seats)[] Positions = [("Mayor", 1), ("Councillor", 6), ("School Trustee", 2)];

    /// <summary>Creates or upgrades the SQLite schema, then seeds municipalities, positions, and candidates when the database is empty.</summary>
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApiDbContext>();
        await db.Database.MigrateAsync();
        // WAL shared-memory files are unreliable on App Service's network-mounted /home storage.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=DELETE;");
        if (await db.Municipalities.AnyAsync()) return;

        foreach (var (name, logo, description) in Municipalities)
        {
            var municipality = new Municipality {
                Name = name, Province = "BC", ElectionDate = new DateTime(2026, 10, 17),
                Description = description, LogoUrl = $"/images/logos/{logo}.png"
            };
            foreach (var (positionName, seats) in Positions)
                municipality.Positions.Add(new Position { Name = positionName, SeatsAvailable = seats });
            db.Municipalities.Add(municipality);
        }
        await db.SaveChangesAsync();

        var positions = await db.Positions.Include(x => x.Municipality).ToListAsync();
        foreach (var sample in DemoCandidates.All)
        {
            var position = positions.Single(x => x.Name == sample.Position && x.Municipality.Name == sample.Municipality);
            db.Candidates.Add(new Candidate {
                Name = sample.Name, Biography = sample.Biography, PhotoUrl = sample.PhotoUrl,
                Website = sample.Website, Email = sample.Email,
                PositionId = position.PositionId, MunicipalityId = position.MunicipalityId
            });
        }
        await db.SaveChangesAsync();
    }
}
