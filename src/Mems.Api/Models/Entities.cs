/*
 * Project: Municipal Elections Companion API (MEMS)
 * Course: COMP4977
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Entity Framework Core entities for municipalities, positions, candidates, users, and voting guides.
 */
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Mems.Api.Models;

/// <summary>Identity user with the display name collected at registration.</summary>
public class ApplicationUser : IdentityUser
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    public List<SavedCandidate> SavedCandidates { get; set; } = [];
}

/// <summary>A community holding a municipal election.</summary>
public class Municipality
{
    public int MunicipalityId { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(50)] public string Province { get; set; } = "BC";
    public DateTime ElectionDate { get; set; } = new(2026, 10, 17);
    [Required, StringLength(2000)] public string Description { get; set; } = "";
    /// <summary>Site-relative (e.g. /images/logos/x.png) or absolute URL; DTOs always expose an absolute URL.</summary>
    [StringLength(500)] public string? LogoUrl { get; set; }
    public List<Position> Positions { get; set; } = [];
}

/// <summary>An office within a municipality and the number of seats to fill.</summary>
public class Position
{
    public int PositionId { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Range(1, 100)] public int SeatsAvailable { get; set; } = 1;
    public int MunicipalityId { get; set; }
    public Municipality Municipality { get; set; } = null!;
    public List<Candidate> Candidates { get; set; } = [];
}

/// <summary>A person running for a position. MunicipalityId mirrors the position's municipality for direct filtering.</summary>
public class Candidate
{
    public int CandidateId { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(4000)] public string Biography { get; set; } = "";
    /// <summary>Site-relative (e.g. /images/demo-candidates/x.jpg) or absolute URL; DTOs always expose an absolute URL.</summary>
    [StringLength(500)] public string? PhotoUrl { get; set; }
    [StringLength(300)] public string? Website { get; set; }
    [StringLength(254)] public string? Email { get; set; }
    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;
    public int MunicipalityId { get; set; }
    public Municipality Municipality { get; set; } = null!;
}

/// <summary>A candidate a user has added to their personal voting guide.</summary>
public class SavedCandidate
{
    public int SavedCandidateId { get; set; }
    [Required] public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public int CandidateId { get; set; }
    public Candidate Candidate { get; set; } = null!;
    public DateTime DateSaved { get; set; }
}
