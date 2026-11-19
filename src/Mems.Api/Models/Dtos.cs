/*
 * Project: Municipal Elections Companion API (MEMS)
 * Course: COMP4977
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Flat request/response contracts so JSON never exposes entity cycles or Identity internals.
 */
using System.ComponentModel.DataAnnotations;

namespace Mems.Api.Models;

public record RegisterRequest(
    [Required(ErrorMessage = "Name is required."), StringLength(100, ErrorMessage = "Name must be 100 characters or fewer.")] string? Name,
    [Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Enter a valid email address.")] string? Email,
    [Required(ErrorMessage = "Password is required."), MinLength(6, ErrorMessage = "Password must be at least 6 characters.")] string? Password);

public record LoginRequest(
    [Required(ErrorMessage = "Email is required.")] string? Email,
    [Required(ErrorMessage = "Password is required.")] string? Password);

public record SaveCandidateRequest([Range(1, int.MaxValue, ErrorMessage = "A valid candidateId is required.")] int CandidateId);

public record MessageResponse(string Message);

public record LoginResponse(string Token, string Name, string Email);

public record MunicipalityDto(int MunicipalityId, string Name, string Province, DateTime ElectionDate, string Description, string? LogoUrl);

public record PositionDto(int PositionId, string Name, int SeatsAvailable, int MunicipalityId, string MunicipalityName);

/// <summary>SeatsAvailable is the candidate's position's seat count, i.e. how many candidates for that office a voter may choose.</summary>
public record CandidateDto(int CandidateId, string Name, string Biography, string? PhotoUrl, string? Website, string? Email,
    int PositionId, string PositionName, int SeatsAvailable, int MunicipalityId, string MunicipalityName);

/// <summary>One municipality's election: the municipality details plus its offices (with seat counts) and how many candidates are running.</summary>
public record ElectionDto(int MunicipalityId, string MunicipalityName, string Province, DateTime ElectionDate, string Description,
    string? LogoUrl, IReadOnlyList<PositionDto> Offices, int CandidateCount);

public record SavedCandidateDto(int SavedCandidateId, string UserId, int CandidateId, DateTime DateSaved, CandidateDto Candidate);
