/*
 * Project: Municipal Elections Companion API (MEMS)
 * Course: COMP4977
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: JWT-protected personal voting guide; every query is scoped to the signed-in user.
 */
using Mems.Api.Data;
using Mems.Api.Models;
using Mems.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Mems.Api.Controllers;

[ApiController]
[Route("api/savedcandidates")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class SavedCandidatesController(ApiDbContext db, DtoMapper mapper, TimeProvider clock) : ControllerBase
{
    private string UserId => User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? throw new InvalidOperationException("Authenticated token has no subject claim.");

    private IQueryable<SavedCandidate> Mine() => db.SavedCandidates
        .Include(x => x.Candidate).ThenInclude(x => x.Position)
        .Include(x => x.Candidate).ThenInclude(x => x.Municipality)
        .Where(x => x.UserId == UserId);

    /// <summary>Lists the signed-in user's voting guide, newest first.</summary>
    [HttpGet]
    public async Task<IEnumerable<SavedCandidateDto>> GetAll() =>
        (await Mine().AsNoTracking().OrderByDescending(x => x.DateSaved).ThenByDescending(x => x.SavedCandidateId).ToListAsync())
            .Select(mapper.Map);

    [HttpGet("{id:int}")]
    [ProducesResponseType<SavedCandidateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavedCandidateDto>> Get(int id) =>
        await Mine().AsNoTracking().SingleOrDefaultAsync(x => x.SavedCandidateId == id) is { } saved
            ? mapper.Map(saved) : NotFound(new MessageResponse("Saved candidate not found"));

    /// <summary>Adds a candidate to the voting guide; returns 409 if it is already saved.</summary>
    [HttpPost]
    [ProducesResponseType<SavedCandidateDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SavedCandidateDto>> Save(SaveCandidateRequest request)
    {
        if (!await db.Candidates.AnyAsync(x => x.CandidateId == request.CandidateId))
            return NotFound(new MessageResponse("Candidate not found"));
        if (await db.SavedCandidates.AnyAsync(x => x.UserId == UserId && x.CandidateId == request.CandidateId))
            return Conflict(new MessageResponse("Candidate already in voting guide"));
        var now = clock.GetUtcNow().UtcDateTime;
        // Whole seconds keep the timestamp compatible with Swift's default ISO 8601 decoding.
        var saved = new SavedCandidate {
            UserId = UserId, CandidateId = request.CandidateId,
            DateSaved = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc)
        };
        db.SavedCandidates.Add(saved);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            // The unique (UserId, CandidateId) index catches a concurrent duplicate save.
            return Conflict(new MessageResponse("Candidate already in voting guide"));
        }
        var dto = mapper.Map(await Mine().AsNoTracking().SingleAsync(x => x.SavedCandidateId == saved.SavedCandidateId));
        return CreatedAtAction(nameof(Get), new { id = dto.SavedCandidateId }, dto);
    }

    /// <summary>Removes an entry by SavedCandidateId, or by CandidateId when no saved entry has that ID.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var saved = await db.SavedCandidates.SingleOrDefaultAsync(x => x.UserId == UserId && x.SavedCandidateId == id)
            ?? await db.SavedCandidates.SingleOrDefaultAsync(x => x.UserId == UserId && x.CandidateId == id);
        if (saved is null) return NotFound(new MessageResponse("Saved candidate not found"));
        db.SavedCandidates.Remove(saved);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
