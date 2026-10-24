/*
 * Project: Municipal Elections Management System (MEMS)
 * Course: COMP4976
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Maps election entities and municipality-linked ASP.NET Core Identity roles.
 */
using Mems.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Mems.Web.Data;

/// <summary>Combines the election model with Identity users, roles, and membership tables.</summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<IdentityUser, ApplicationRole, string>(options)
{
    public DbSet<Municipality> Municipalities => Set<Municipality>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Candidate> Candidates => Set<Candidate>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Municipality>().HasIndex(x => x.Name).IsUnique();
        builder.Entity<Position>().HasIndex(x => new { x.MunicipalityId, x.Name }).IsUnique();
        // Restrictive parent deletes let controllers require explicit candidate reassignment or removal.
        builder.Entity<Position>().HasOne(x => x.Municipality).WithMany(x => x.Positions)
            .HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Candidate>().HasOne(x => x.Position).WithMany(x => x.Candidates)
            .HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
        // A unique nullable seed key identifies managed samples while ordinary candidates retain null keys.
        builder.Entity<Candidate>().HasIndex(x => x.DemoSeedKey).IsUnique();
        builder.Entity<ApplicationRole>().HasOne(x => x.Municipality).WithMany()
            .HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
    }
}
