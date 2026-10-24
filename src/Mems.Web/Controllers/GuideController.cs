/*
 * Project: Municipal Elections Management System (MEMS)
 * Course: COMP4976
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Manages anonymous voting-guide selections and the printable guide.
 */
using Mems.Web.Data;
using Mems.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mems.Web.Controllers;

/// <summary>Resolves session choices against current candidates and enforces available seat limits.</summary>
public class GuideController(ApplicationDbContext db, VotingSelection selection) : Controller
{
    public async Task<IActionResult> Index()
    {
        var ids = selection.Read();
        var candidates = await db.Candidates.AsNoTracking().Include(x => x.Position).ThenInclude(x => x.Municipality)
            .Where(x => ids.Contains(x.CandidateId)).OrderBy(x => x.Position.Municipality.Name).ThenBy(x => x.Position.Name).ThenBy(x => x.Name).ToListAsync();
        // Prune deleted candidates from this session without changing any stored election records.
        selection.Save(candidates.Select(x => x.CandidateId).ToHashSet());
        return View(candidates);
    }
    /// <summary>Adds a current candidate within its position's seat limit and the session's overall size cap.</summary>
    [HttpPost]
    public async Task<IActionResult> Add(int id, string? returnUrl)
    {
        var candidate = await db.Candidates.Include(x => x.Position).SingleOrDefaultAsync(x => x.CandidateId == id);
        if (candidate is null) return NotFound();
        var ids = selection.Read();
        var positionIds = await db.Candidates.Where(x => ids.Contains(x.CandidateId) && x.PositionId == candidate.PositionId).Select(x => x.CandidateId).ToListAsync();
        if (!ids.Contains(id) && positionIds.Count >= candidate.Position.SeatsAvailable)
            TempData["Notice"] = $"You have selected all {candidate.Position.SeatsAvailable} available seat(s) for {candidate.Position.Name}. Remove a choice first.";
        else if (ids.Count >= 200) TempData["Notice"] = "Your guide has reached its 200-candidate limit.";
        else { ids.Add(id); selection.Save(ids); TempData["Notice"] = "Candidate added to your voting guide."; }
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public IActionResult Remove(int id)
    {
        var ids = selection.Read(); ids.Remove(id); selection.Save(ids);
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public IActionResult Clear() { selection.Save([]); return RedirectToAction(nameof(Index)); }
}
