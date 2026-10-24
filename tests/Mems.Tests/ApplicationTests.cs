using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Mems.Web.Data;
using Mems.Web.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Mems.Tests;

public class MemsFactory : WebApplicationFactory<Program>
{
    public string TestRoot { get; } = Path.Combine(Path.GetTempPath(), "mems-tests-" + Guid.NewGuid().ToString("N"));
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(TestRoot);
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={Path.Combine(TestRoot, "tests.db")}");
        builder.UseSetting("Storage:Root", TestRoot);
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
    }
    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
    public async Task<T> Query<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }
}

public class ApplicationTests(MemsFactory app) : IClassFixture<MemsFactory>
{
    private static async Task<string> Token(HttpClient browser, string page)
    {
        var response = await browser.GetAsync(page);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Anti-forgery token missing on " + page);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    private static async Task<HttpResponseMessage> Post(HttpClient browser, string path, string tokenPage, Dictionary<string,string>? values = null)
    {
        values ??= [];
        values["__RequestVerificationToken"] = await Token(browser, tokenPage);
        return await browser.PostAsync(path, new FormUrlEncodedContent(values));
    }
    private static async Task Login(HttpClient browser, string username)
    {
        var response = await Post(browser, "/Account/Login", "/Account/Login", new() { ["UserName"] = username, ["Password"] = "P@$$w0rd" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Admin", response.Headers.Location!.ToString());
    }
    private async Task<(int Own, int Foreign, int OwnPosition, int ForeignPosition, int OwnCandidate, int ForeignCandidate)> Ids() => await app.Query(async db => {
        var own = await db.Municipalities.SingleAsync(x => x.Name == "Pitt Meadows");
        var foreign = await db.Municipalities.SingleAsync(x => x.Name == "Maple Ridge");
        var ownPosition = await db.Positions.FirstAsync(x => x.MunicipalityId == own.MunicipalityId && x.Name == "Mayor");
        var foreignPosition = await db.Positions.FirstAsync(x => x.MunicipalityId == foreign.MunicipalityId && x.Name == "Mayor");
        return (own.MunicipalityId, foreign.MunicipalityId, ownPosition.PositionId, foreignPosition.PositionId,
            (await db.Candidates.FirstAsync(x => x.PositionId == ownPosition.PositionId)).CandidateId,
            (await db.Candidates.FirstAsync(x => x.PositionId == foreignPosition.PositionId)).CandidateId);
    });

    [Fact]
    public async Task Public_directory_filters_profiles_and_anonymous_guide_work()
    {
        using var browser = app.Browser();
        var ids = await Ids();
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/")).StatusCode);
        var html = await browser.GetStringAsync($"/Directory?municipalityId={ids.Own}&positionId={ids.OwnPosition}");
        Assert.Contains("2 candidate(s) found", html);
        Assert.Contains("data-val", await browser.GetStringAsync("/Account/Login"));
        Assert.Contains("No matching candidates", await browser.GetStringAsync("/Directory?search=zzzznonexistent"));
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/Directory/Details/{ids.OwnCandidate}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/Directory/Details/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/Municipalities/Details/{ids.Own}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(browser, "/Guide/Add", "/Directory", new() { ["id"] = ids.OwnCandidate.ToString() })).StatusCode);
        var guide = await browser.GetStringAsync("/Guide");
        Assert.Contains("Pitt Meadows", guide); Assert.Contains("October 17, 2026", guide); Assert.Contains("Mayor", guide); Assert.Contains("Prepared / print date:", guide);
        Assert.Contains("print-guide", guide);
        using var independent = app.Browser();
        Assert.Contains("Your guide is ready", await independent.GetStringAsync("/Guide"));
        var secondMayor = await app.Query(db => db.Candidates.Where(x => x.PositionId == ids.OwnPosition && x.CandidateId != ids.OwnCandidate).Select(x => x.CandidateId).FirstAsync());
        await Post(browser, "/Guide/Add", "/Directory", new() { ["id"] = secondMayor.ToString() });
        Assert.Contains("Remove a choice first", await browser.GetStringAsync("/Guide"));
        await Post(browser, "/Guide/Remove", "/Guide", new() { ["id"] = ids.OwnCandidate.ToString() });
        Assert.Contains("Your guide is ready", await browser.GetStringAsync("/Guide"));
        await Post(browser, "/Guide/Add", "/Directory", new() { ["id"] = ids.ForeignCandidate.ToString(), ["returnUrl"] = "https://evil.example" });
        await Post(browser, "/Guide/Clear", "/Guide");
        Assert.Contains("Your guide is ready", await browser.GetStringAsync("/Guide"));
    }

    [Fact]
    public async Task Identity_roles_migrations_and_domain_seed_are_present()
    {
        using var browser = app.Browser();
        Assert.Equal(4, await app.Query(db => db.Municipalities.CountAsync()));
        Assert.True(await app.Query(db => db.Candidates.CountAsync()) >= 24);
        Assert.NotEmpty(await app.Query(async db => (await db.Database.GetAppliedMigrationsAsync()).ToArray()));
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var pm = await users.FindByNameAsync("pm");
        Assert.NotNull(pm); Assert.True(await users.IsInRoleAsync(pm, "Pitt Meadows Admin"));
        var super = await users.FindByNameAsync("super");
        Assert.NotNull(super); Assert.True(await users.IsInRoleAsync(super, "Super Admin"));
        Assert.True(await app.Query(db => db.Roles.AnyAsync(x => x.Name == "Pitt Meadows Admin" && x.MunicipalityId != null)));
    }

    [Fact]
    public async Task Anonymous_admin_requests_are_challenged_and_modifying_posts_require_antiforgery()
    {
        using var browser = app.Browser();
        var response = await browser.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync("/Guide/Add", new FormUrlEncodedContent(new Dictionary<string,string> { ["id"] = "1" }))).StatusCode);
        await Login(browser, "super");
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync("/Admin/Positions/Delete/1", new FormUrlEncodedContent([]))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(browser, "/Account/Logout", "/Admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/Admin")).StatusCode);
    }

    [Fact]
    public async Task Municipality_admin_is_isolated_on_lists_gets_and_forged_mutations()
    {
        using var browser = app.Browser(); await Login(browser, "pm");
        var ids = await Ids();
        Assert.Contains("only your assigned", await browser.GetStringAsync("/Admin"));
        Assert.DoesNotContain("Maple Ridge", await browser.GetStringAsync("/Admin/Positions"));
        Assert.DoesNotContain("Maple Ridge", await browser.GetStringAsync("/Admin/Candidates"));
        foreach (var action in new[] { "Details", "Edit", "Delete" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/Admin/Candidates/{action}/{ids.ForeignCandidate}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/Admin/Positions/{action}/{ids.ForeignPosition}")).StatusCode);
        }
        var forbidden = await browser.GetAsync("/Admin/Municipalities");
        Assert.Equal(HttpStatusCode.Redirect, forbidden.StatusCode); Assert.Contains("AccessDenied", forbidden.Headers.Location!.ToString());
        var ownForm = $"/Admin/Candidates/Edit/{ids.OwnCandidate}";
        var forged = await Post(browser, $"/Admin/Candidates/Edit/{ids.OwnCandidate}", ownForm, new() {
            ["CandidateId"] = ids.OwnCandidate.ToString(), ["Name"] = "Forged", ["Profile"] = "Unauthorized move", ["MunicipalityId"] = ids.Foreign.ToString(), ["PositionId"] = ids.ForeignPosition.ToString() });
        Assert.Equal(HttpStatusCode.Redirect, forged.StatusCode); Assert.Contains("AccessDenied", forged.Headers.Location!.ToString());
        var mixed = await Post(browser, $"/Admin/Candidates/Edit/{ids.OwnCandidate}", ownForm, new() {
            ["CandidateId"] = ids.OwnCandidate.ToString(), ["Name"] = "Forged", ["Profile"] = "Mismatched parent", ["MunicipalityId"] = ids.Own.ToString(), ["PositionId"] = ids.ForeignPosition.ToString() });
        Assert.Contains("belonging to the selected municipality", await mixed.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await Post(browser, $"/Admin/Candidates/Delete/{ids.ForeignCandidate}", ownForm)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(browser, $"/Admin/Positions/Edit/{ids.ForeignPosition}", "/Admin/Positions/Create", new() {
            ["PositionId"] = ids.ForeignPosition.ToString(), ["Name"] = "Forged", ["SeatsAvailable"] = "1", ["MunicipalityId"] = ids.Own.ToString() })).StatusCode);
        var foreignCreate = await Post(browser, "/Admin/Positions/Create", "/Admin/Positions/Create", new() {
            ["Name"] = "Forged", ["SeatsAvailable"] = "1", ["MunicipalityId"] = ids.Foreign.ToString() });
        Assert.Contains("AccessDenied", foreignCreate.Headers.Location!.ToString());
        Assert.False(await app.Query(db => db.Candidates.AnyAsync(x => x.Name == "Forged")));
    }

    [Fact]
    public async Task Municipal_admin_can_create_edit_and_delete_positions_and_candidates_with_validation()
    {
        using var browser = app.Browser(); await Login(browser, "pm"); var ids = await Ids();
        var name = "Test position " + Guid.NewGuid().ToString("N");
        var positionFields = new Dictionary<string,string> { ["PositionId"] = "0", ["Name"] = name, ["SeatsAvailable"] = "0", ["MunicipalityId"] = ids.Own.ToString() };
        var invalid = await Post(browser, "/Admin/Positions/Create", "/Admin/Positions/Create", positionFields);
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode); Assert.Contains("between 1 and 100", await invalid.Content.ReadAsStringAsync());
        positionFields["SeatsAvailable"] = "2";
        Assert.Equal(HttpStatusCode.Redirect, (await Post(browser, "/Admin/Positions/Create", "/Admin/Positions/Create", positionFields)).StatusCode);
        var positionId = await app.Query(db => db.Positions.Where(x => x.Name == name).Select(x => x.PositionId).SingleAsync());
        var fields = new Dictionary<string,string> { ["CandidateId"] = "0", ["Name"] = "Test candidate " + Guid.NewGuid().ToString("N"), ["Profile"] = "Testing biography.", ["PositionId"] = positionId.ToString(), ["MunicipalityId"] = ids.Own.ToString(), ["Email"] = "bad-address" };
        var badEmail = await Post(browser, "/Admin/Candidates/Create", "/Admin/Candidates/Create", fields);
        Assert.Equal(HttpStatusCode.OK, badEmail.StatusCode); Assert.Contains("not a valid e-mail", await badEmail.Content.ReadAsStringAsync());
        fields["Email"] = "test@example.org"; fields["Website"] = "javascript:alert(1)";
        Assert.Equal(HttpStatusCode.OK, (await Post(browser, "/Admin/Candidates/Create", "/Admin/Candidates/Create", fields)).StatusCode);
        fields["Website"] = "https://example.org";
        Assert.Equal(HttpStatusCode.Redirect, (await Post(browser, "/Admin/Candidates/Create", "/Admin/Candidates/Create", fields)).StatusCode);
        var candidate = await app.Query(db => db.Candidates.SingleAsync(x => x.Name == fields["Name"]));
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/Admin/Candidates/Details/{candidate.CandidateId}")).StatusCode);
        var blocked = await Post(browser, $"/Admin/Positions/Delete/{positionId}", $"/Admin/Positions/Delete/{positionId}");
        Assert.Contains("Delete or reassign", await blocked.Content.ReadAsStringAsync());
        fields["CandidateId"] = candidate.CandidateId.ToString(); fields["Profile"] = "Updated biography.";
        Assert.Equal(HttpStatusCode.Redirect, (await Post(browser, $"/Admin/Candidates/Edit/{candidate.CandidateId}", $"/Admin/Candidates/Edit/{candidate.CandidateId}", fields)).StatusCode);
        Assert.Equal("Updated biography.", await app.Query(db => db.Candidates.Where(x => x.CandidateId == candidate.CandidateId).Select(x => x.Profile).SingleAsync()));
        Assert.Equal(HttpStatusCode.Redirect, (await Post(browser, $"/Admin/Candidates/Delete/{candidate.CandidateId}", $"/Admin/Candidates/Delete/{candidate.CandidateId}")).StatusCode);
        positionFields["PositionId"] = positionId.ToString(); positionFields["SeatsAvailable"] = "3";
        Assert.Equal(HttpStatusCode.Redirect, (await Post(browser, $"/Admin/Positions/Edit/{positionId}", $"/Admin/Positions/Edit/{positionId}", positionFields)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(browser, $"/Admin/Positions/Delete/{positionId}", $"/Admin/Positions/Delete/{positionId}")).StatusCode);
        Assert.False(await app.Query(db => db.Positions.AnyAsync(x => x.PositionId == positionId)));
    }

    [Fact]
    public async Task Super_admin_has_global_access_and_municipality_crud_creates_and_removes_group()
    {
        using var browser = app.Browser(); await Login(browser, "super");
        var ids = await Ids();
        Assert.Contains("Maple Ridge", await browser.GetStringAsync("/Admin/Positions"));
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/Admin/Candidates/Edit/{ids.ForeignCandidate}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/Admin/Positions/Edit/{ids.ForeignPosition}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/Admin/Municipalities/Details/{ids.Foreign}")).StatusCode);
        var name = "Test municipality " + Guid.NewGuid().ToString("N");
        var fields = new Dictionary<string,string> { ["MunicipalityId"] = "0", ["Name"] = name, ["Province"] = "British Columbia", ["ElectionDate"] = "2026-10-17", ["Description"] = "Test community" };
        Assert.Equal(HttpStatusCode.Redirect, (await Post(browser, "/Admin/Municipalities/Create", "/Admin/Municipalities/Create", fields)).StatusCode);
        var municipalityId = await app.Query(db => db.Municipalities.Where(x => x.Name == name).Select(x => x.MunicipalityId).SingleAsync());
        Assert.True(await app.Query(db => db.Roles.AnyAsync(x => x.MunicipalityId == municipalityId)));
        fields["MunicipalityId"] = municipalityId.ToString(); fields["Description"] = "Edited description";
        Assert.Equal(HttpStatusCode.Redirect, (await Post(browser, $"/Admin/Municipalities/Edit/{municipalityId}", $"/Admin/Municipalities/Edit/{municipalityId}", fields)).StatusCode);
        Assert.Equal("Edited description", await app.Query(db => db.Municipalities.Where(x => x.MunicipalityId == municipalityId).Select(x => x.Description).SingleAsync()));
        var blocked = await Post(browser, $"/Admin/Municipalities/Delete/{ids.Own}", $"/Admin/Municipalities/Delete/{ids.Own}");
        Assert.Contains("Remove positions", await blocked.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await Post(browser, $"/Admin/Municipalities/Delete/{municipalityId}", $"/Admin/Municipalities/Delete/{municipalityId}")).StatusCode);
        Assert.False(await app.Query(db => db.Municipalities.AnyAsync(x => x.MunicipalityId == municipalityId)));
        Assert.False(await app.Query(db => db.Roles.AnyAsync(x => x.MunicipalityId == municipalityId)));
    }

    [Fact]
    public async Task Login_rejects_wrong_password_and_external_redirect()
    {
        using var browser = app.Browser();
        var wrong = await Post(browser, "/Account/Login", "/Account/Login", new() { ["UserName"] = "pm", ["Password"] = "incorrect" });
        Assert.Contains("Invalid username or password", await wrong.Content.ReadAsStringAsync());
        var correct = await Post(browser, "/Account/Login", "/Account/Login", new() { ["UserName"] = "pm", ["Password"] = "P@$$w0rd", ["ReturnUrl"] = "https://evil.example" });
        Assert.Equal("/Admin", correct.Headers.Location!.ToString());
    }

    public static async Task<HttpResponseMessage> Upload(HttpClient browser, string path, string page, Dictionary<string,string> fields, string filename, byte[] bytes)
    {
        using var content = new MultipartFormDataContent();
        foreach (var (key,value) in fields) content.Add(new StringContent(value), key);
        content.Add(new StringContent(await Token(browser, page)), "__RequestVerificationToken");
        var file = new ByteArrayContent(bytes); file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "Image", filename);
        return await browser.PostAsync(path, content);
    }

    [Fact]
    public async Task Upload_rejects_spoofed_and_oversized_files_and_safely_stores_logo_and_photo()
    {
        using var browser = app.Browser(); await Login(browser, "super"); var ids = await Ids();
        var fields = new Dictionary<string,string> { ["CandidateId"] = ids.ForeignCandidate.ToString(), ["Name"] = "Upload test", ["Profile"] = "A test profile", ["MunicipalityId"] = ids.Foreign.ToString(), ["PositionId"] = ids.ForeignPosition.ToString() };
        var path = $"/Admin/Candidates/Edit/{ids.ForeignCandidate}";
        var spoofed = await Upload(browser, path, path, fields, "fake.png", "<script>evil</script>"u8.ToArray());
        Assert.Contains("not a valid image", await spoofed.Content.ReadAsStringAsync());
        var large = await Upload(browser, path, path, fields, "large.png", new byte[2097153]);
        Assert.Contains("2 MB", await large.Content.ReadAsStringAsync());
        var svg = await Upload(browser, path, path, fields, "danger.svg", "<svg/>"u8.ToArray());
        Assert.Contains("Only JPEG and PNG", await svg.Content.ReadAsStringAsync());
        // Input name is deliberately hostile; stored names must be generated.
        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(SKColors.Teal);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var png = encoded.ToArray();
        var mismatched = await Upload(browser, path, path, fields, "portrait.jpg", png);
        Assert.Contains("contents must match", await mismatched.Content.ReadAsStringAsync());
        var valid = await Upload(browser, path, path, fields, "../../portrait.png", png);
        Assert.Equal(HttpStatusCode.Redirect, valid.StatusCode);
        var stored = await app.Query(db => db.Candidates.Where(x => x.CandidateId == ids.ForeignCandidate).Select(x => x.PhotoFileName).SingleAsync());
        Assert.Matches("^[a-f0-9]{32}\\.png$", stored!);
        Assert.Equal("image/png", (await browser.GetAsync("/media/" + stored)).Content.Headers.ContentType!.MediaType);
        var m = await app.Query(db => db.Municipalities.SingleAsync(x => x.MunicipalityId == ids.Foreign));
        var mfields = new Dictionary<string,string> { ["MunicipalityId"] = m.MunicipalityId.ToString(), ["Name"] = m.Name, ["Province"] = m.Province, ["Description"] = m.Description, ["ElectionDate"] = "2026-10-17" };
        var mpath = $"/Admin/Municipalities/Edit/{m.MunicipalityId}";
        Assert.Equal(HttpStatusCode.Redirect, (await Upload(browser, mpath, mpath, mfields, "logo.png", png)).StatusCode);
        var logo = await app.Query(db => db.Municipalities.Where(x => x.MunicipalityId == ids.Foreign).Select(x => x.LogoFileName).SingleAsync());
        Assert.Matches("^[a-f0-9]{32}\\.png$", logo!);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/media/" + logo)).StatusCode);
        mfields["RemoveImage"] = "true";
        Assert.Equal(HttpStatusCode.Redirect, (await Post(browser, mpath, mpath, mfields)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/media/" + logo)).StatusCode);
        Assert.Null(await app.Query(db => db.Municipalities.Where(x => x.MunicipalityId == ids.Foreign).Select(x => x.LogoFileName).SingleAsync()));
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/media/not-a-generated-name.png")).StatusCode);
    }

    [Fact]
    public async Task Required_and_length_validation_and_route_id_checks_prevent_invalid_creates()
    {
        using var browser = app.Browser(); await Login(browser, "pm"); var ids = await Ids();
        var count = await app.Query(db => db.Candidates.CountAsync());
        var fields = new Dictionary<string,string> { ["CandidateId"] = "0", ["Name"] = "", ["Profile"] = new string('x', 4001), ["MunicipalityId"] = ids.Own.ToString(), ["PositionId"] = ids.OwnPosition.ToString() };
        var invalid = await Post(browser, "/Admin/Candidates/Create", "/Admin/Candidates/Create", fields);
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var html = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("Name field is required", html); Assert.Contains("maximum length", html);
        fields["Name"] = "Required parent test"; fields["Profile"] = "Biography"; fields["MunicipalityId"] = "0"; fields["PositionId"] = "0";
        var parent = await Post(browser, "/Admin/Candidates/Create", "/Admin/Candidates/Create", fields);
        Assert.Equal(HttpStatusCode.OK, parent.StatusCode);
        Assert.Contains("belonging to the selected municipality", await parent.Content.ReadAsStringAsync());
        fields["CandidateId"] = ids.OwnCandidate.ToString();
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(browser, "/Admin/Candidates/Create", "/Admin/Candidates/Create", fields)).StatusCode);
        var position = await Post(browser, "/Admin/Positions/Create", "/Admin/Positions/Create", new() { ["PositionId"] = "0", ["MunicipalityId"] = ids.Own.ToString(), ["SeatsAvailable"] = "1" });
        Assert.Equal(HttpStatusCode.OK, position.StatusCode); Assert.Contains("Name field is required", await position.Content.ReadAsStringAsync());
        Assert.Equal(count, await app.Query(db => db.Candidates.CountAsync()));
    }
}

