/*
 * Project: Municipal Elections Companion API (MEMS)
 * Course: COMP4977
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Converts entities to flat DTOs and stored image paths to absolute URLs for the iOS client.
 */
using Mems.Api.Models;

namespace Mems.Api.Services;

/// <summary>Builds DTOs for the current request so image URLs use the scheme and host the client actually called.</summary>
public class DtoMapper(IHttpContextAccessor accessor, IConfiguration config)
{
    /// <summary>Returns absolute URLs unchanged and prefixes site-relative paths with the public base URL.</summary>
    public string? AbsoluteUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https") return path;
        return $"{BaseUrl()}/{path.TrimStart('/')}";
    }

    // App:PublicBaseUrl pins the host (e.g. behind a custom domain); otherwise use {Request.Scheme}://{Request.Host}.
    private string BaseUrl()
    {
        var configured = config["App:PublicBaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured)) return configured.TrimEnd('/');
        var request = accessor.HttpContext?.Request ?? throw new InvalidOperationException("Image URLs require an HTTP request.");
        return $"{request.Scheme}://{request.Host}{request.PathBase}";
    }

    public MunicipalityDto Map(Municipality m) =>
        new(m.MunicipalityId, m.Name, m.Province, m.ElectionDate, m.Description, AbsoluteUrl(m.LogoUrl));

    /// <summary>Requires Position.Municipality to be loaded.</summary>
    public PositionDto Map(Position p) =>
        new(p.PositionId, p.Name, p.SeatsAvailable, p.MunicipalityId, p.Municipality.Name);

    /// <summary>Requires Candidate.Position and Candidate.Municipality to be loaded.</summary>
    public CandidateDto Map(Candidate c) =>
        new(c.CandidateId, c.Name, c.Biography, AbsoluteUrl(c.PhotoUrl), c.Website, c.Email,
            c.PositionId, c.Position.Name, c.MunicipalityId, c.Municipality.Name);

    /// <summary>Requires the candidate graph described on <see cref="Map(Candidate)"/>.</summary>
    public SavedCandidateDto Map(SavedCandidate s) =>
        new(s.SavedCandidateId, s.UserId, s.CandidateId, s.DateSaved, Map(s.Candidate));
}
