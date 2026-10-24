/*
 * Project: Municipal Elections Management System (MEMS)
 * Course: COMP4976
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Provides municipality-scoped candidate CRUD and photograph replacement.
 */
using Mems.Web.Data;
using Mems.Web.Models;
using Mems.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Mems.Web.Areas.Admin.Controllers;

/// <summary>Restricts candidate reads and mutations through AdminScope and validates destination ownership.</summary>
[Area("Admin"), Authorize(Policy = "Admin")]
public class CandidatesController(ApplicationDbContext db, AdminScope scope, ImageStore images) : Controller
{
    public async Task<IActionResult> Index(string? search)
    {
        var query = scope.Candidates.Include(x => x.Position).ThenInclude(x => x.Municipality).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.ToLower().Contains(search.ToLower()));
        ViewBag.Search = search;
        return View(await query.OrderBy(x => x.Name).ToListAsync());
    }
    public async Task<IActionResult> Details(int id)
    {
        var item = await scope.Candidates.Include(x => x.Position).ThenInclude(x => x.Municipality).SingleOrDefaultAsync(x => x.CandidateId == id);
        return item is null ? NotFound() : View(item);
    }
    public async Task<IActionResult> Create() => View("Form", await Options(new CandidateForm()));
    public async Task<IActionResult> Edit(int id)
    {
        var item = await scope.Candidates.Include(x => x.Position).SingleOrDefaultAsync(x => x.CandidateId == id);
        return item is null ? NotFound() : View("Form", await Options(new CandidateForm { CandidateId = id, Name = item.Name, Profile = item.Profile,
            Email = item.Email, Website = item.Website, PositionId = item.PositionId, MunicipalityId = item.Position.MunicipalityId, CurrentImage = item.PhotoFileName }));
    }
    [HttpPost] public async Task<IActionResult> Create(CandidateForm form) => await Save(0, form);
    [HttpPost] public async Task<IActionResult> Edit(int id, CandidateForm form) => await Save(id, form);
    /// <summary>Shares validated create/edit handling while checking both existing-record and destination scope.</summary>
    private async Task<IActionResult> Save(int id, CandidateForm form)
    {
        if (id != form.CandidateId) return BadRequest();
        // Load the existing candidate through the scoped query before accepting edits or reassignment.
        var item = id == 0 ? new Candidate() : await scope.Candidates.SingleOrDefaultAsync(x => x.CandidateId == id);
        if (item is null) return NotFound();
        if (form.MunicipalityId > 0 && !await scope.CanManage(form.MunicipalityId)) return Forbid();
        // Both ownership and parent/child consistency must hold for submitted IDs.
        if (!await scope.Positions.AnyAsync(x => x.PositionId == form.PositionId && x.MunicipalityId == form.MunicipalityId))
            ModelState.AddModelError("PositionId", "Choose a position belonging to the selected municipality.");
        if (!string.IsNullOrWhiteSpace(form.Website) && (!Uri.TryCreate(form.Website, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")))
            ModelState.AddModelError("Website", "Use a complete http:// or https:// website address.");
        form.CurrentImage = item.PhotoFileName;
        string? uploaded = null;
        if (ModelState.IsValid && form.Image is not null)
        {
            try { uploaded = await images.Save(form.Image); }
            catch (InvalidDataException ex) { ModelState.AddModelError("Image", ex.Message); }
        }
        if (!ModelState.IsValid) return View("Form", await Options(form));
        var oldImage = item.PhotoFileName;
        item.Name = form.Name.Trim(); item.Profile = form.Profile.Trim(); item.Website = form.Website; item.Email = form.Email; item.PositionId = form.PositionId;
        item.PhotoFileName = uploaded ?? (form.RemoveImage ? null : oldImage);
        if (id == 0) db.Add(item);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            images.Delete(uploaded);
            ModelState.AddModelError("", "The candidate or position changed. Refresh and try again.");
            return View("Form", await Options(form));
        }
        // Remove the superseded upload only after the candidate update has committed successfully.
        if (oldImage != item.PhotoFileName) images.Delete(oldImage);
        TempData["Notice"] = "Candidate saved.";
        return RedirectToAction(nameof(Index));
    }
    public async Task<IActionResult> Delete(int id)
    {
        var item = await scope.Candidates.Include(x => x.Position).ThenInclude(x => x.Municipality).SingleOrDefaultAsync(x => x.CandidateId == id);
        return item is null ? NotFound() : View(item);
    }
    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await scope.Candidates.SingleOrDefaultAsync(x => x.CandidateId == id);
        if (item is null) return NotFound();
        db.Remove(item);
        try { await db.SaveChangesAsync(); images.Delete(item.PhotoFileName); }
        catch (DbUpdateException) { TempData["Notice"] = "The candidate changed. Refresh and try again."; }
        return RedirectToAction(nameof(Index));
    }
    // Rebuild dropdowns from authorized queries, including when redisplaying an invalid form.
    private async Task<CandidateForm> Options(CandidateForm form)
    {
        form.Municipalities = await scope.Municipalities.OrderBy(x => x.Name).Select(x => new SelectListItem(x.Name, x.MunicipalityId.ToString())).ToListAsync();
        form.Positions = await scope.Positions.Include(x => x.Municipality).OrderBy(x => x.Name).ToListAsync();
        return form;
    }
}
