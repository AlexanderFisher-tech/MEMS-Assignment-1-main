/*
 * Project: Municipal Elections Management System (MEMS)
 * Course: COMP4976
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Defines municipality, position, candidate, and administrative role entities.
 */
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Mems.Web.Models;

/// <summary>Represents a community and the election positions it administers.</summary>
public class Municipality
{
    public int MunicipalityId { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(50)] public string Province { get; set; } = "British Columbia";
    [DataType(DataType.Date)] public DateOnly ElectionDate { get; set; } = new(2026, 10, 17);
    [Required, StringLength(2000)] public string Description { get; set; } = "";
    public string? LogoFileName { get; set; }
    public List<Position> Positions { get; set; } = [];
}

/// <summary>Defines a municipality-owned office and its available seat count.</summary>
public class Position
{
    public int PositionId { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Range(1, 100), Display(Name = "Seats available")] public int SeatsAvailable { get; set; } = 1;
    public int MunicipalityId { get; set; }
    public Municipality Municipality { get; set; } = null!;
    public List<Candidate> Candidates { get; set; } = [];
}

/// <summary>Represents a candidate whose municipality is determined by the associated position.</summary>
public class Candidate
{
    public int CandidateId { get; set; }
    // Provenance only: never accepted from an administrator's edit form.
    [StringLength(80)] public string? DemoSeedKey { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(4000)] public string Profile { get; set; } = "";
    public string? PhotoFileName { get; set; }
    [StringLength(300), Url] public string? Website { get; set; }
    [StringLength(254), EmailAddress] public string? Email { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;
}

// Each municipality gets a distinct Identity role bound to its database ID.
public class ApplicationRole : IdentityRole
{
    public int? MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
}
