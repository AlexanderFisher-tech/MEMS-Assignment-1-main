/*
 * Project: Municipal Elections Management System (MEMS)
 * Course: COMP4976
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Serves approved packaged portraits and validated administrator uploads.
 */
using System.Text.RegularExpressions;
using Mems.Web.Services;
using Mems.Web.Data;
using Microsoft.AspNetCore.Mvc;

namespace Mems.Web.Controllers;

/// <summary>Maps allowlisted image names to known storage roots rather than accepting client paths.</summary>
public partial class MediaController(ImageStore store, IWebHostEnvironment environment) : Controller
{
    [HttpGet("media/{name}")]
    public IActionResult Image(string name)
    {
        if (name == "sample-avatar.svg") return PhysicalFile(Path.Combine(environment.WebRootPath, "images", name), "image/svg+xml");
        if (DemoCandidates.IsPortrait(name))
        {
            var portrait = Path.Combine(environment.WebRootPath, "images", "demo-candidates", name);
            return System.IO.File.Exists(portrait) ? PhysicalFile(portrait, "image/jpeg") : NotFound();
        }
        if (!SafeName().IsMatch(name)) return NotFound();
        var path = Path.Combine(store.Root, name);
        return System.IO.File.Exists(path) ? PhysicalFile(path, "image/png") : NotFound();
    }
    [GeneratedRegex("^[a-f0-9]{32}\\.png$")] private static partial Regex SafeName();
}
