/*
 * Project: Municipal Elections Companion API (MEMS)
 * Course: COMP4977
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Resolves the ?city= / ?municipalityId= query parameters that scope election data to one municipality.
 */
using Mems.Api.Data;
using Mems.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mems.Api.Services;

public class MunicipalityScope(ApiDbContext db)
{
    public const string MissingMessage = "Specify a municipality with ?city={name} (for example ?city=Burnaby) or ?municipalityId={id}.";

    /// <summary>
    /// Finds the municipality named by <paramref name="city"/> (case-insensitive) or <paramref name="municipalityId"/>.
    /// Returns an error result when the municipality is unknown, the two parameters disagree,
    /// or neither is given and <paramref name="required"/> is true.
    /// </summary>
    public async Task<(Municipality? Municipality, ActionResult? Error)> ResolveAsync(string? city, int? municipalityId, bool required)
    {
        city = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        if (city is null && municipalityId is null)
            return (null, required ? new BadRequestObjectResult(new MessageResponse(MissingMessage)) : null);

        Municipality? byName = null;
        if (city is not null)
        {
            var lowered = city.ToLower();
            byName = await db.Municipalities.AsNoTracking().SingleOrDefaultAsync(x => x.Name.ToLower() == lowered);
            if (byName is null) return (null, new NotFoundObjectResult(new MessageResponse($"Municipality '{city}' not found")));
        }
        if (municipalityId is null) return (byName, null);

        var byId = await db.Municipalities.AsNoTracking().SingleOrDefaultAsync(x => x.MunicipalityId == municipalityId);
        if (byId is null) return (null, new NotFoundObjectResult(new MessageResponse("Municipality not found")));
        if (byName is not null && byName.MunicipalityId != byId.MunicipalityId)
            return (null, new BadRequestObjectResult(new MessageResponse("city and municipalityId refer to different municipalities.")));
        return (byId, null);
    }
}
