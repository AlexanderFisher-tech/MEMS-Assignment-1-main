/*
 * Project: Municipal Elections Management System (MEMS)
 * Course: COMP4976
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Serves public municipality browsing and elected-position information.
 */
using Mems.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mems.Web.Controllers;

/// <summary>Provides read-only public community and position browsing.</summary>
public class MunicipalitiesController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index() => View(await db.Municipalities.AsNoTracking().OrderBy(x => x.Name).ToListAsync());
    public async Task<IActionResult> Details(int id)
    {
        var municipality = await db.Municipalities.AsNoTracking().AsSplitQuery().Include(x => x.Positions).ThenInclude(x => x.Candidates).SingleOrDefaultAsync(x => x.MunicipalityId == id);
        return municipality is null ? NotFound() : View(municipality);
    }
}
