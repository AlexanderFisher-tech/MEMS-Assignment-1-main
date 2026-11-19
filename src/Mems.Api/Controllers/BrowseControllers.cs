/*
 * Project: Municipal Elections Companion API (MEMS)
 * Course: COMP4977
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Public, read-only endpoints for municipalities, offices (positions), elections, and candidates.
 *              Election data is always scoped to one municipality chosen with ?city= or ?municipalityId=.
 */
using Mems.Api.Data;
using Mems.Api.Models;
using Mems.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mems.Api.Controllers;

[ApiController]
[Route("api/municipalities")]
[Produces("application/json")]
public class MunicipalitiesController(ApiDbContext db, DtoMapper mapper) : ControllerBase
{
    /// <summary>Lists every municipality (used by the app to let a user choose their city).</summary>
    [HttpGet]
    public async Task<IEnumerable<MunicipalityDto>> GetAll() =>
        (await db.Municipalities.AsNoTracking().OrderBy(x => x.Name).ToListAsync()).Select(mapper.Map);

    [HttpGet("{id:int}")]
    [ProducesResponseType<MunicipalityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MunicipalityDto>> Get(int id) =>
        await db.Municipalities.AsNoTracking().SingleOrDefaultAsync(x => x.MunicipalityId == id) is { } m
            ? mapper.Map(m) : NotFound(new MessageResponse("Municipality not found"));
}

[ApiController]
[Route("api/elections")]
[Produces("application/json")]
public class ElectionsController(ApiDbContext db, MunicipalityScope scope, DtoMapper mapper) : ControllerBase
{
    /// <summary>Returns the election for one municipality: date, details, offices with seat counts, and candidate count.</summary>
    [HttpGet]
    [ProducesResponseType<ElectionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ElectionDto>> Get(string? city = null, int? municipalityId = null)
    {
        var (municipality, error) = await scope.ResolveAsync(city, municipalityId, required: true);
        if (error is not null) return error;
        var offices = await db.Positions.AsNoTracking().Include(x => x.Municipality)
            .Where(x => x.MunicipalityId == municipality!.MunicipalityId).OrderBy(x => x.PositionId).ToListAsync();
        var candidateCount = await db.Candidates.CountAsync(x => x.MunicipalityId == municipality!.MunicipalityId);
        return mapper.Map(municipality!, offices, candidateCount);
    }
}

/// <summary>Positions are also exposed as "offices" (the term used in the Assignment 2 specification).</summary>
[ApiController]
[Route("api/positions")]
[Route("api/offices")]
[Produces("application/json")]
public class PositionsController(ApiDbContext db, MunicipalityScope scope, DtoMapper mapper) : ControllerBase
{
    /// <summary>Lists the offices of one municipality. Requires ?city= or ?municipalityId=.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<PositionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<PositionDto>>> GetAll(string? city = null, int? municipalityId = null)
    {
        var (municipality, error) = await scope.ResolveAsync(city, municipalityId, required: true);
        if (error is not null) return error;
        var positions = await db.Positions.AsNoTracking().Include(x => x.Municipality)
            .Where(x => x.MunicipalityId == municipality!.MunicipalityId).OrderBy(x => x.PositionId).ToListAsync();
        return positions.Select(mapper.Map).ToList();
    }

    /// <summary>Returns one office. With ?city= or ?municipalityId=, an office from another municipality is reported as not found.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<PositionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionDto>> Get(int id, string? city = null, int? municipalityId = null)
    {
        var (municipality, error) = await scope.ResolveAsync(city, municipalityId, required: false);
        if (error is not null) return error;
        var position = await db.Positions.AsNoTracking().Include(x => x.Municipality).SingleOrDefaultAsync(x => x.PositionId == id);
        if (position is null || (municipality is not null && position.MunicipalityId != municipality.MunicipalityId))
            return NotFound(new MessageResponse(municipality is null ? "Position not found" : $"Position not found in {municipality.Name}"));
        return mapper.Map(position);
    }
}

[ApiController]
[Route("api/candidates")]
[Produces("application/json")]
public class CandidatesController(ApiDbContext db, MunicipalityScope scope, DtoMapper mapper) : ControllerBase
{
    /// <summary>Lists the candidates of one municipality, optionally for one office. Requires ?city= or ?municipalityId=.</summary>
    [HttpGet]
    [ProducesResponseType<List<CandidateDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public Task<ActionResult<List<CandidateDto>>> GetAll(string? city = null, int? municipalityId = null, int? positionId = null) =>
        Find(null, city, municipalityId, positionId);

    /// <summary>Case-insensitive partial match on candidate name within one municipality; an empty name returns all of that municipality's candidates.</summary>
    [HttpGet("search")]
    [ProducesResponseType<List<CandidateDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public Task<ActionResult<List<CandidateDto>>> Search(string? name = null, string? city = null, int? municipalityId = null, int? positionId = null) =>
        Find(name, city, municipalityId, positionId);

    /// <summary>Returns one candidate. With ?city= or ?municipalityId=, a candidate from another municipality is reported as not found.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<CandidateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CandidateDto>> Get(int id, string? city = null, int? municipalityId = null)
    {
        var (municipality, error) = await scope.ResolveAsync(city, municipalityId, required: false);
        if (error is not null) return error;
        var candidate = await WithDetails().SingleOrDefaultAsync(x => x.CandidateId == id);
        if (candidate is null || (municipality is not null && candidate.MunicipalityId != municipality.MunicipalityId))
            return NotFound(new MessageResponse(municipality is null ? "Candidate not found" : $"Candidate not found in {municipality.Name}"));
        return mapper.Map(candidate);
    }

    private IQueryable<Candidate> WithDetails() =>
        db.Candidates.AsNoTracking().Include(x => x.Position).Include(x => x.Municipality);

    private async Task<ActionResult<List<CandidateDto>>> Find(string? name, string? city, int? municipalityId, int? positionId)
    {
        var (municipality, error) = await scope.ResolveAsync(city, municipalityId, required: true);
        if (error is not null) return error;
        var query = WithDetails().Where(x => x.MunicipalityId == municipality!.MunicipalityId);
        if (!string.IsNullOrWhiteSpace(name))
        {
            var term = name.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(term));
        }
        if (positionId is not null) query = query.Where(x => x.PositionId == positionId);
        var candidates = await query.OrderBy(x => x.PositionId).ThenBy(x => x.Name).ToListAsync();
        return candidates.Select(mapper.Map).ToList();
    }
}
