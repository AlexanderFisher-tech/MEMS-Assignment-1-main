/*
 * Project: Municipal Elections Management System (MEMS)
 * Course: COMP4976
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Provides Super Admin municipality CRUD and linked role creation.
 */
using Mems.Web.Data;
using Mems.Web.Models;
using Mems.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mems.Web.Areas.Admin.Controllers;

/// <summary>Reserves municipality and associated administrator-group management for Super Admin.</summary>
[Area("Admin"), Authorize(Roles = AdminScope.SuperRole)]
public class MunicipalitiesController(ApplicationDbContext db, RoleManager<ApplicationRole> roles, ImageStore images) : Controller
{
    public async Task<IActionResult> Index() => View(await db.Municipalities.OrderBy(x => x.Name).ToListAsync());
    public async Task<IActionResult> Details(int id)
    {
        var item = await db.Municipalities.Include(x => x.Positions).SingleOrDefaultAsync(x => x.MunicipalityId == id);
        return item is null ? NotFound() : View(item);
    }
    public IActionResult Create() => View("Form", new MunicipalityForm());
    public async Task<IActionResult> Edit(int id)
    {
        var item = await db.Municipalities.FindAsync(id);
        return item is null ? NotFound() : View("Form", new MunicipalityForm { MunicipalityId = id, Name = item.Name, Province = item.Province,
            ElectionDate = item.ElectionDate, Description = item.Description, CurrentImage = item.LogoFileName });
    }
    [HttpPost] public async Task<IActionResult> Create(MunicipalityForm form) => await Save(0, form);
    [HttpPost] public async Task<IActionResult> Edit(int id, MunicipalityForm form) => await Save(id, form);
    /// <summary>Shares validated create/edit handling and commits new municipalities with their administrator role.</summary>
    private async Task<IActionResult> Save(int id, MunicipalityForm form)
    {
        if (id != form.MunicipalityId) return BadRequest();
        var item = id == 0 ? new Municipality() : await db.Municipalities.FindAsync(id);
        if (item is null) return NotFound();
        form.Name = form.Name?.Trim() ?? "";
        if (await db.Municipalities.AnyAsync(x => x.MunicipalityId != id && x.Name.ToLower() == form.Name.ToLower()))
            ModelState.AddModelError("Name", "A municipality with this name already exists.");
        form.CurrentImage = item.LogoFileName;
        string? uploaded = null;
        if (ModelState.IsValid && form.Image is not null)
        {
            try { uploaded = await images.Save(form.Image); }
            catch (InvalidDataException ex) { ModelState.AddModelError("Image", ex.Message); }
        }
        if (!ModelState.IsValid) return View("Form", form);
        var oldImage = item.LogoFileName;
        item.Name = form.Name; item.Province = form.Province.Trim(); item.Description = form.Description.Trim(); item.ElectionDate = form.ElectionDate!.Value;
        item.LogoFileName = uploaded ?? (form.RemoveImage ? null : oldImage);
        if (id == 0) db.Add(item);
        // Keep municipality creation and its Identity role atomic; a failed role must not leave an orphan setup.
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            await db.SaveChangesAsync();
            if (id == 0)
            {
                var result = await roles.CreateAsync(new ApplicationRole { Name = item.Name + " Admin", MunicipalityId = item.MunicipalityId });
                if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
            }
            await transaction.CommitAsync();
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
        {
            await transaction.RollbackAsync(); images.Delete(uploaded);
            ModelState.AddModelError("", "The municipality or its admin group could not be saved. Check for a duplicate name and try again.");
            return View("Form", form);
        }
        if (oldImage != item.LogoFileName) images.Delete(oldImage);
        TempData["Notice"] = "Municipality saved. Its administrator group remains linked by municipality ID even if its name changes.";
        return RedirectToAction(nameof(Index));
    }
    public async Task<IActionResult> Delete(int id)
    {
        var item = await db.Municipalities.Include(x => x.Positions).SingleOrDefaultAsync(x => x.MunicipalityId == id);
        return item is null ? NotFound() : View(item);
    }
    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await db.Municipalities.Include(x => x.Positions).SingleOrDefaultAsync(x => x.MunicipalityId == id);
        if (item is null) return NotFound();
        // Dependent positions or assigned administrators must be dealt with before deleting the parent.
        var municipalRoles = await db.Roles.Where(x => x.MunicipalityId == id).ToListAsync();
        var roleIds = municipalRoles.Select(x => x.Id).ToList();
        if (item.Positions.Count > 0 || await db.UserRoles.AnyAsync(x => roleIds.Contains(x.RoleId)))
        { ModelState.AddModelError("", "Remove positions and administrator memberships before deleting this municipality."); return View("Delete", item); }
        db.Roles.RemoveRange(municipalRoles); db.Remove(item);
        try { await db.SaveChangesAsync(); images.Delete(item.LogoFileName); }
        catch (DbUpdateException) { TempData["Notice"] = "The municipality is in use or changed. Refresh and try again."; }
        return RedirectToAction(nameof(Index));
    }
}
