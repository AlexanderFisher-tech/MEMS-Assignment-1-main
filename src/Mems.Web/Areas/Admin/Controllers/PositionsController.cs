/*
 * Project: Municipal Elections Management System (MEMS)
 * Course: COMP4976
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Provides municipality-scoped position CRUD and seat validation.
 */
using Mems.Web.Data;
using Mems.Web.Models;
using Mems.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Mems.Web.Areas.Admin.Controllers;

/// <summary>Restricts position reads and writes to the administrator's permitted municipalities.</summary>
[Area("Admin"), Authorize(Policy = "Admin")]
public class PositionsController(ApplicationDbContext db, AdminScope scope) : Controller
{
    public async Task<IActionResult> Index() => View(await scope.Positions.Include(x => x.Municipality).OrderBy(x => x.Municipality.Name).ThenBy(x => x.Name).ToListAsync());
    public async Task<IActionResult> Details(int id)
    {
        var item = await scope.Positions.Include(x => x.Municipality).Include(x => x.Candidates).SingleOrDefaultAsync(x => x.PositionId == id);
        return item is null ? NotFound() : View(item);
    }
    public async Task<IActionResult> Create() => View("Form", await Options(new PositionForm()));
    public async Task<IActionResult> Edit(int id)
    {
        var item = await scope.Positions.SingleOrDefaultAsync(x => x.PositionId == id);
        return item is null ? NotFound() : View("Form", await Options(new PositionForm { PositionId = id, Name = item.Name, MunicipalityId = item.MunicipalityId, SeatsAvailable = item.SeatsAvailable }));
    }
    [HttpPost]
    public async Task<IActionResult> Create(PositionForm form) => await Save(0, form);
    [HttpPost]
    public async Task<IActionResult> Edit(int id, PositionForm form) => await Save(id, form);
    /// <summary>Shares validated create/edit handling while checking both existing-record and destination scope.</summary>
    private async Task<IActionResult> Save(int id, PositionForm form)
    {
        if (id != form.PositionId) return BadRequest();
        var item = id == 0 ? new Position() : await scope.Positions.SingleOrDefaultAsync(x => x.PositionId == id);
        if (item is null) return NotFound();
        if (form.MunicipalityId > 0 && !await scope.CanManage(form.MunicipalityId)) return Forbid();
        // Moving a populated position would also move candidate ownership, so reassignment is blocked.
        if (id != 0 && item.MunicipalityId != form.MunicipalityId && await db.Candidates.AnyAsync(x => x.PositionId == id))
            ModelState.AddModelError("MunicipalityId", "A position with candidates cannot move to another municipality.");
        form.Name = form.Name?.Trim() ?? "";
        if (await db.Positions.AnyAsync(x => x.PositionId != id && x.MunicipalityId == form.MunicipalityId && x.Name.ToLower() == form.Name.ToLower()))
            ModelState.AddModelError("Name", "This municipality already has a position with this name.");
        if (!ModelState.IsValid) return View("Form", await Options(form));
        item.Name = form.Name; item.SeatsAvailable = form.SeatsAvailable; item.MunicipalityId = form.MunicipalityId;
        if (id == 0) db.Add(item);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { ModelState.AddModelError("", "The record changed or the name is already used. Refresh and try again."); return View("Form", await Options(form)); }
        TempData["Notice"] = "Position saved.";
        return RedirectToAction(nameof(Index));
    }
    public async Task<IActionResult> Delete(int id)
    {
        var item = await scope.Positions.Include(x => x.Municipality).Include(x => x.Candidates).SingleOrDefaultAsync(x => x.PositionId == id);
        return item is null ? NotFound() : View(item);
    }
    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await scope.Positions.Include(x => x.Municipality).Include(x => x.Candidates).SingleOrDefaultAsync(x => x.PositionId == id);
        if (item is null) return NotFound();
        if (item.Candidates.Count != 0) { ModelState.AddModelError("", "Delete or reassign the candidates before deleting this position."); return View("Delete", item); }
        db.Remove(item);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { TempData["Notice"] = "Position could not be deleted because it is in use or changed."; }
        return RedirectToAction(nameof(Index));
    }
    private async Task<PositionForm> Options(PositionForm form)
    {
        form.Municipalities = await scope.Municipalities.OrderBy(x => x.Name).Select(x => new SelectListItem(x.Name, x.MunicipalityId.ToString())).ToListAsync();
        return form;
    }
}
