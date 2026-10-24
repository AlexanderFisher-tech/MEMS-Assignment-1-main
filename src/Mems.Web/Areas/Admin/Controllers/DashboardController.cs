/*
 * Project: Municipal Elections Management System (MEMS)
 * Course: COMP4976
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Displays election counts within the signed-in administrator's scope.
 */
using Mems.Web.Models;
using Mems.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mems.Web.Areas.Admin.Controllers;

/// <summary>Reports only counts from the administrator's authorized municipality queries.</summary>
[Area("Admin"), Authorize(Policy = "Admin")]
public class DashboardController(AdminScope scope) : Controller
{
    public async Task<IActionResult> Index() => View(new DashboardViewModel(await scope.Municipalities.CountAsync(), await scope.Positions.CountAsync(), await scope.Candidates.CountAsync()));
}
