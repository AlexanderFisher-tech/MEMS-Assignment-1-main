/*
 * Project: Municipal Elections Management System (MEMS)
 * Course: COMP4976
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Serves anonymous candidate search, filtering, pagination, and profiles.
 */
using Mems.Web.Data;
using Mems.Web.Models;
using Mems.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mems.Web.Controllers;

/// <summary>Exposes read-only public candidate browsing without requiring authentication.</summary>
public class DirectoryController(ApplicationDbContext db, VotingSelection selection) : Controller
{
    public async Task<IActionResult> Index(string? search, int? municipalityId, int? positionId, int page = 1)
    {
        var query = db.Candidates.AsNoTracking().Include(x => x.Position).ThenInclude(x => x.Municipality).AsQueryable();
        search = search?.Trim();
        if (!string.IsNullOrEmpty(search)) query = query.Where(x => x.Name.ToLower().Contains(search.ToLower()));
        if (municipalityId.HasValue) query = query.Where(x => x.Position.MunicipalityId == municipalityId);
        if (positionId.HasValue) query = query.Where(x => x.PositionId == positionId);
        // Apply every filter before counting and paging so result totals match the displayed subset.
        var count = await query.CountAsync();
        var totalPages = Math.Max(1, (count + 11) / 12);
        page = Math.Clamp(page, 1, totalPages);
        return View(new DirectoryViewModel {
            Search = search, MunicipalityId = municipalityId, PositionId = positionId,
            Municipalities = await db.Municipalities.AsNoTracking().OrderBy(x => x.Name).ToListAsync(),
            Positions = await db.Positions.AsNoTracking().Include(x => x.Municipality).Where(x => !municipalityId.HasValue || x.MunicipalityId == municipalityId).OrderBy(x => x.Name).ToListAsync(),
            Candidates = await query.OrderBy(x => x.Name).ThenBy(x => x.CandidateId).Skip((page - 1) * 12).Take(12).ToListAsync(),
            SelectedIds = selection.Read(), Page = page, TotalPages = totalPages, TotalCount = count });
    }
    public async Task<IActionResult> Details(int id)
    {
        var candidate = await db.Candidates.AsNoTracking().Include(x => x.Position).ThenInclude(x => x.Municipality).SingleOrDefaultAsync(x => x.CandidateId == id);
        if (candidate is null) return NotFound();
        ViewBag.Selected = selection.Read().Contains(id);
        return View(candidate);
    }
}
