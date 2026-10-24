/*
 * Project: Municipal Elections Companion API (MEMS)
 * Course: COMP4977
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Maps election entities, saved voting-guide entries, and ASP.NET Core Identity users to SQLite.
 */
using Mems.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Mems.Api.Data;

public class ApiDbContext(DbContextOptions<ApiDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Municipality> Municipalities => Set<Municipality>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<SavedCandidate> SavedCandidates => Set<SavedCandidate>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Municipality>().HasIndex(x => x.Name).IsUnique();
        builder.Entity<Position>().HasIndex(x => new { x.MunicipalityId, x.Name }).IsUnique();
        builder.Entity<Position>().HasOne(x => x.Municipality).WithMany(x => x.Positions)
            .HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Candidate>().HasOne(x => x.Position).WithMany(x => x.Candidates)
            .HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Candidate>().HasOne(x => x.Municipality).WithMany()
            .HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        // A user can save each candidate once; removing a user or candidate removes their guide entries.
        builder.Entity<SavedCandidate>().HasIndex(x => new { x.UserId, x.CandidateId }).IsUnique();
        builder.Entity<SavedCandidate>().HasOne(x => x.User).WithMany(x => x.SavedCandidates)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<SavedCandidate>().HasOne(x => x.Candidate).WithMany()
            .HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Cascade);
        // SQLite drops DateTimeKind, so mark saved timestamps as UTC on read to serialize with a trailing Z.
        builder.Entity<SavedCandidate>().Property(x => x.DateSaved)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
    }
}
