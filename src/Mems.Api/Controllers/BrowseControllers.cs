/*
 * Project: Municipal Elections Companion API (MEMS)
 * Course: COMP4977
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Public, read-only endpoints for municipalities, positions, and candidates.
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
[Route("api/positions")]
[Produces("application/json")]
public class PositionsController(ApiDbContext db, DtoMapper mapper) : ControllerBase
{
    /// <summary>Lists positions, optionally for one municipality.</summary>
    [HttpGet]
    public async Task<IEnumerable<PositionDto>> GetAll(int? municipalityId = null)
    {
        var query = db.Positions.AsNoTracking().Include(x => x.Municipality).AsQueryable();
        if (municipalityId is not null) query = query.Where(x => x.MunicipalityId == municipalityId);
        return (await query.OrderBy(x => x.Municipality.Name).ThenBy(x => x.PositionId).ToListAsync()).Select(mapper.Map);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<PositionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionDto>> Get(int id) =>
        await db.Positions.AsNoTracking().Include(x => x.Municipality).SingleOrDefaultAsync(x => x.PositionId == id) is { } p
            ? mapper.Map(p) : NotFound(new MessageResponse("Position not found"));
}

[ApiController]
[Route("api/candidates")]
[Produces("application/json")]
public class CandidatesController(ApiDbContext db, DtoMapper mapper) : ControllerBase
{
    /// <summary>Lists candidates, optionally filtered by municipality and/or position.</summary>
    [HttpGet]
    public Task<List<CandidateDto>> GetAll(int? municipalityId = null, int? positionId = null) =>
        Find(null, municipalityId, positionId);

    /// <summary>Case-insensitive partial match on candidate name; an empty name returns every candidate.</summary>
    [HttpGet("search")]
    public Task<List<CandidateDto>> Search(string? name = null, int? municipalityId = null, int? positionId = null) =>
        Find(name, municipalityId, positionId);

    [HttpGet("{id:int}")]
    [ProducesResponseType<CandidateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CandidateDto>> Get(int id) =>
        await WithDetails().SingleOrDefaultAsync(x => x.CandidateId == id) is { } c
            ? mapper.Map(c) : NotFound(new MessageResponse("Candidate not found"));

    private IQueryable<Candidate> WithDetails() =>
        db.Candidates.AsNoTracking().Include(x => x.Position).Include(x => x.Municipality);

    private async Task<List<CandidateDto>> Find(string? name, int? municipalityId, int? positionId)
    {
        var query = WithDetails();
        if (!string.IsNullOrWhiteSpace(name))
        {
            var term = name.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(term));
        }
        if (municipalityId is not null) query = query.Where(x => x.MunicipalityId == municipalityId);
        if (positionId is not null) query = query.Where(x => x.PositionId == positionId);
        var candidates = await query.OrderBy(x => x.Municipality.Name).ThenBy(x => x.PositionId).ThenBy(x => x.Name).ToListAsync();
        return candidates.Select(mapper.Map).ToList();
    }
}
