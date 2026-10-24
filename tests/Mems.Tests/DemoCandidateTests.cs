using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mems.Web.Data;
using Mems.Web.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Mems.Tests;

public class DemoCandidateTests
{
    [Fact]
    public async Task Guide_groups_multiple_untracked_candidates_by_municipality_and_position_ids()
    {
        await using var app = new MemsFactory();
        using var browser = app.Browser();
        var html = await browser.GetStringAsync("/Directory");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var ids = await app.Query(db => db.Candidates.Where(x => x.Position.Municipality.Name == "Pitt Meadows" && x.Position.Name == "Councilor")
            .Select(x => x.CandidateId).ToListAsync());
        foreach (var id in ids)
            Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync("/Guide/Add", new FormUrlEncodedContent(new Dictionary<string,string> {
                ["id"] = id.ToString(), ["__RequestVerificationToken"] = token }))).StatusCode);
        var guide = await browser.GetStringAsync("/Guide");
        Assert.Single(Regex.Matches(guide, "class=\"panel guide-group"));
        Assert.Single(Regex.Matches(guide, "class=\"guide-position-heading\""));
        Assert.Equal(2, Regex.Matches(guide, "class=\"guide-record\"").Count);
        Assert.Contains("2 selected / 6 seat(s)", guide);
        Assert.Contains("fictional demonstration content", guide);
        Assert.Equal(2, Regex.Matches(guide, "class=\"prose\"").Count);
    }

    [Fact]
    public async Task Every_sample_has_unique_content_and_a_distinct_working_synthetic_portrait()
    {
        await using var app = new MemsFactory();
        using var browser = app.Browser();
        var candidates = await app.Query(db => db.Candidates.OrderBy(x => x.CandidateId).ToListAsync());
        Assert.Equal(24, candidates.Count);
        Assert.Equal(24, candidates.Select(x => x.Name).Distinct().Count());
        Assert.Equal(24, candidates.Select(x => x.DemoSeedKey).Distinct().Count());
        var hashes = new HashSet<string>();
        foreach (var candidate in candidates)
        {
            Assert.InRange(Regex.Matches(candidate.Profile, @"\S+").Count, 80, 150);
            Assert.Contains("fictional", candidate.Profile, StringComparison.OrdinalIgnoreCase);
            Assert.True(DemoCandidates.IsPortrait(candidate.PhotoFileName!));
            var response = await browser.GetAsync("/media/" + candidate.PhotoFileName);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("image/jpeg", response.Content.Headers.ContentType!.MediaType);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.True(hashes.Add(Convert.ToHexString(SHA256.HashData(bytes))));
            using var bitmap = SKBitmap.Decode(bytes);
            Assert.NotNull(bitmap); Assert.Equal(480, bitmap.Width); Assert.Equal(600, bitmap.Height);
            var details = await browser.GetStringAsync($"/Directory/Details/{candidate.CandidateId}");
            Assert.Contains(candidate.Profile[..70], details);
            Assert.Contains("portrait is AI-generated", details);
            Assert.Contains("Fictional demonstration candidate", details);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/media/demo-nonexistent.jpg")).StatusCode);
        var first = candidates.First();
        Assert.Contains(first.Profile[..70], await browser.GetStringAsync("/Directory?search=" + Uri.EscapeDataString(first.Name)));
    }

    [Fact]
    public async Task Legacy_upgrade_is_repeatable_and_preserves_edits_uploads_unrelated_rows_and_deleted_samples()
    {
        await using var app = new MemsFactory();
        using var browser = app.Browser();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var candidates = await db.Candidates.Include(x => x.Position).ThenInclude(x => x.Municipality).OrderBy(x => x.CandidateId).ToListAsync();
        // Reconstruct the original seed format in an isolated test database, never the real workspace DB.
        foreach (var candidate in candidates)
        {
            candidate.DemoSeedKey = null;
            candidate.Name = DemoCandidates.LegacyName(candidate.PositionId, (candidate.CandidateId - 1) % 2);
            candidate.Profile = DemoCandidates.LegacyBiography(candidate.Position.Name, candidate.Position.Municipality.Name);
            candidate.PhotoFileName = "sample-avatar.svg";
        }
        candidates[1].Profile = "An administrator's custom biography must survive.";
        candidates[2].PhotoFileName = new string('a', 32) + ".png";
        candidates[3].PhotoFileName = null; // Deliberately removed photograph.
        var uploadPath = Path.Combine(app.TestRoot, "uploads", candidates[2].PhotoFileName!);
        Directory.CreateDirectory(Path.GetDirectoryName(uploadPath)!);
        await File.WriteAllTextAsync(uploadPath, "Existing uploaded bytes must survive unchanged.");
        var originalDates = candidates.ToDictionary(x => x.CandidateId, x => x.DateCreated);
        var unrelated = new Candidate { Name = candidates[0].Name, Profile = candidates[0].Profile, PositionId = candidates[0].PositionId,
            PhotoFileName = "sample-avatar.svg", Email = "candidate@example.org", Website = "https://example.org" };
        db.Add(unrelated);
        await db.SaveChangesAsync();
        await DemoCandidates.UpgradeLegacy(db);
        Assert.Equal("Mara Ellison", candidates[0].Name);
        Assert.Equal("An administrator's custom biography must survive.", candidates[1].Profile);
        Assert.Null(candidates[1].DemoSeedKey);
        Assert.Equal(new string('a', 32) + ".png", candidates[2].PhotoFileName);
        Assert.Null(candidates[3].PhotoFileName);
        Assert.Null(unrelated.DemoSeedKey);
        Assert.Equal(DemoCandidates.LegacyName(1, 0), unrelated.Name);
        Assert.Equal(25, await db.Candidates.CountAsync());
        Assert.All(candidates, c => Assert.Equal(originalDates[c.CandidateId], c.DateCreated));
        // Once claimed, even a new administrator name/profile/photo must never be refreshed by seeding.
        candidates[0].Name = "My custom name"; candidates[0].Profile = "My custom edited biography";
        candidates[0].PhotoFileName = new string('b', 32) + ".png";
        db.Remove(candidates[4]);
        await db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(await db.Candidates.AsNoTracking().OrderBy(x => x.CandidateId)
            .Select(x => new { x.CandidateId, x.Name, x.Profile, x.PhotoFileName, x.DateCreated, x.DemoSeedKey, x.Email, x.Website, x.PositionId }).ToListAsync());
        await SeedData.Initialize(scope.ServiceProvider, true, scope.ServiceProvider.GetRequiredService<IConfiguration>());
        await SeedData.Initialize(scope.ServiceProvider, true, scope.ServiceProvider.GetRequiredService<IConfiguration>());
        var after = JsonSerializer.Serialize(await db.Candidates.AsNoTracking().OrderBy(x => x.CandidateId)
            .Select(x => new { x.CandidateId, x.Name, x.Profile, x.PhotoFileName, x.DateCreated, x.DemoSeedKey, x.Email, x.Website, x.PositionId }).ToListAsync());
        Assert.Equal(before, after);
        Assert.Equal("Existing uploaded bytes must survive unchanged.", await File.ReadAllTextAsync(uploadPath));
    }

    [Fact]
    public async Task Admin_can_replace_a_synthetic_portrait_twice_and_reseeding_preserves_the_upload()
    {
        await using var app = new MemsFactory();
        using var browser = app.Browser();
        var loginHtml = await browser.GetStringAsync("/Account/Login");
        var token = Regex.Match(loginHtml, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var login = await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["UserName"] = "pm", ["Password"] = "P@$$w0rd", ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token) }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var candidate = await app.Query(db => db.Candidates.Include(x => x.Position).SingleAsync(x => x.DemoSeedKey == "mara-ellison"));
        var form = new Dictionary<string,string> {
            ["CandidateId"] = candidate.CandidateId.ToString(), ["Name"] = candidate.Name,
            ["Profile"] = "An administrator changed this biography.", ["MunicipalityId"] = candidate.Position.MunicipalityId.ToString(), ["PositionId"] = candidate.PositionId.ToString() };
        using var bitmap = new SKBitmap(20, 25);
        var editPath = $"/Admin/Candidates/Edit/{candidate.CandidateId}";
        async Task<string> Replace(SKColor color)
        {
            bitmap.Erase(color);
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            var response = await ApplicationTests.Upload(browser, editPath, editPath, form, "replacement.png", encoded.ToArray());
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            return (await app.Query(db => db.Candidates.Where(x => x.CandidateId == candidate.CandidateId).Select(x => x.PhotoFileName).SingleAsync()))!;
        }
        var first = await Replace(SKColors.Teal);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/media/" + first)).StatusCode);
        var second = await Replace(SKColors.SteelBlue);
        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/media/" + first)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/media/" + second)).StatusCode);
        // Shared packaged assets remain available; only the superseded upload is removed.
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/media/" + candidate.PhotoFileName)).StatusCode);
        using var scope = app.Services.CreateScope();
        await SeedData.Initialize(scope.ServiceProvider, true, scope.ServiceProvider.GetRequiredService<IConfiguration>());
        var after = await app.Query(db => db.Candidates.SingleAsync(x => x.CandidateId == candidate.CandidateId));
        Assert.Equal("mara-ellison", after.DemoSeedKey);
        Assert.Equal(second, after.PhotoFileName);
        Assert.Equal(form["Profile"], after.Profile);
    }
}
