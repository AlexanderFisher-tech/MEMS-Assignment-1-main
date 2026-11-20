using Mems.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Mems.Api.Tests;

public class MigrationTests
{
    [Fact]
    public async Task Existing_databases_are_upgraded_to_five_councillor_seats()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mems-migration-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<ApiDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            await using var db = new ApiDbContext(options);
            var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();

            // A database deployed before the change: initial schema, seeded with 6 councillor seats.
            await migrator.MigrateAsync("20261024070201_InitialCreate");
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO Municipalities (MunicipalityId, Name, Province, ElectionDate, Description) VALUES (1, 'Burnaby', 'BC', '2026-10-17', 'Test');
                INSERT INTO Positions (PositionId, Name, SeatsAvailable, MunicipalityId) VALUES (1, 'Mayor', 1, 1), (2, 'Councillor', 6, 1), (3, 'School Trustee', 2, 1);
                """);

            await db.Database.MigrateAsync();

            var seats = await db.Positions.OrderBy(x => x.PositionId).Select(x => x.SeatsAvailable).ToListAsync();
            Assert.Equal(new[] { 1, 5, 2 }, seats);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
