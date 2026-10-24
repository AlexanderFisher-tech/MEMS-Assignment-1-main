using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Mems.Api.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    public string TestRoot { get; } = Path.Combine(Path.GetTempPath(), "mems-api-tests-" + Guid.NewGuid().ToString("N"));
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(TestRoot);
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={Path.Combine(TestRoot, "api-tests.db")};Pooling=False");
        builder.UseSetting("Storage:Root", TestRoot);
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
    }
    public HttpClient Api() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(TestRoot, true); } catch (IOException) { }
    }
}

public class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "password123";

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static string[] Keys(JsonElement element) => element.EnumerateObject().Select(x => x.Name).Order().ToArray();

    private static void AssertKeys(JsonElement element, params string[] expected) => Assert.Equal(expected.Order().ToArray(), Keys(element));

    private static readonly string[] CandidateKeys = ["candidateId", "name", "biography", "photoUrl", "website", "email",
        "positionId", "positionName", "municipalityId", "municipalityName"];

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private async Task<(string Token, string Email)> RegisterAndLogin(HttpClient api, string name = "Jane Doe")
    {
        var email = NewEmail();
        var register = await api.PostAsJsonAsync("/api/auth/register", new { name, email, password = Password });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var login = await api.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return ((await Json(login)).GetProperty("token").GetString()!, email);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    [Fact]
    public async Task Municipalities_return_seeded_data_with_absolute_logo_urls()
    {
        using var api = factory.Api();
        var response = await api.GetAsync("/api/municipalities");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = (await Json(response)).EnumerateArray().ToList();
        foreach (var name in new[] { "Pitt Meadows", "Maple Ridge", "Burnaby" })
            Assert.Contains(list, x => x.GetProperty("name").GetString() == name);
        var burnaby = list.Single(x => x.GetProperty("name").GetString() == "Burnaby");
        AssertKeys(burnaby, "municipalityId", "name", "province", "electionDate", "description", "logoUrl");
        Assert.Equal("BC", burnaby.GetProperty("province").GetString());
        Assert.Equal("2026-10-17T00:00:00", burnaby.GetProperty("electionDate").GetString());
        var logoUrl = burnaby.GetProperty("logoUrl").GetString()!;
        Assert.StartsWith("https://localhost/images/logos/", logoUrl);

        var logo = await api.GetAsync(new Uri(logoUrl).PathAndQuery);
        Assert.Equal(HttpStatusCode.OK, logo.StatusCode);
        Assert.Equal("image/png", logo.Content.Headers.ContentType!.MediaType);

        var id = burnaby.GetProperty("municipalityId").GetInt32();
        var single = await api.GetAsync($"/api/municipalities/{id}");
        Assert.Equal(HttpStatusCode.OK, single.StatusCode);
        Assert.Equal("Burnaby", (await Json(single)).GetProperty("name").GetString());
        var missing = await api.GetAsync("/api/municipalities/99999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("Municipality not found", (await Json(missing)).GetProperty("message").GetString());
    }

    [Fact]
    public async Task Positions_include_required_offices_and_municipality_names()
    {
        using var api = factory.Api();
        var list = (await Json(await api.GetAsync("/api/positions"))).EnumerateArray().ToList();
        foreach (var name in new[] { "Mayor", "Councillor", "School Trustee" })
            Assert.Contains(list, x => x.GetProperty("name").GetString() == name);
        var mayor = list.First(x => x.GetProperty("name").GetString() == "Mayor");
        AssertKeys(mayor, "positionId", "name", "seatsAvailable", "municipalityId", "municipalityName");
        Assert.Equal(1, mayor.GetProperty("seatsAvailable").GetInt32());
        Assert.False(string.IsNullOrEmpty(mayor.GetProperty("municipalityName").GetString()));

        var single = await api.GetAsync($"/api/positions/{mayor.GetProperty("positionId").GetInt32()}");
        Assert.Equal(HttpStatusCode.OK, single.StatusCode);
        Assert.Equal("Mayor", (await Json(single)).GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetAsync("/api/positions/99999")).StatusCode);
    }

    [Fact]
    public async Task Candidates_list_details_and_photos_are_public()
    {
        using var api = factory.Api();
        var list = (await Json(await api.GetAsync("/api/candidates"))).EnumerateArray().ToList();
        Assert.True(list.Count >= 10);
        var first = list[0];
        AssertKeys(first, CandidateKeys);
        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("biography").GetString()));
        var photoUrl = first.GetProperty("photoUrl").GetString()!;
        Assert.StartsWith("https://localhost/images/demo-candidates/", photoUrl);
        var photo = await api.GetAsync(new Uri(photoUrl).PathAndQuery);
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/jpeg", photo.Content.Headers.ContentType!.MediaType);

        var id = first.GetProperty("candidateId").GetInt32();
        var single = await Json(await api.GetAsync($"/api/candidates/{id}"));
        Assert.Equal(first.GetProperty("name").GetString(), single.GetProperty("name").GetString());
        var missing = await api.GetAsync("/api/candidates/99999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("Candidate not found", (await Json(missing)).GetProperty("message").GetString());

        // Optional filters for the iOS directory.
        var municipalityId = first.GetProperty("municipalityId").GetInt32();
        var filtered = (await Json(await api.GetAsync($"/api/candidates?municipalityId={municipalityId}"))).EnumerateArray().ToList();
        Assert.NotEmpty(filtered);
        Assert.All(filtered, x => Assert.Equal(municipalityId, x.GetProperty("municipalityId").GetInt32()));
    }

    [Fact]
    public async Task Candidate_search_is_case_insensitive_partial_and_empty_returns_all()
    {
        using var api = factory.Api();
        var all = (await Json(await api.GetAsync("/api/candidates"))).GetArrayLength();
        var results = (await Json(await api.GetAsync("/api/candidates/search?name=ELLI"))).EnumerateArray().ToList();
        Assert.Contains(results, x => x.GetProperty("name").GetString() == "Mara Ellison");
        Assert.All(results, x => Assert.Contains("elli", x.GetProperty("name").GetString()!, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(all, (await Json(await api.GetAsync("/api/candidates/search?name="))).GetArrayLength());
        Assert.Equal(all, (await Json(await api.GetAsync("/api/candidates/search"))).GetArrayLength());
        Assert.Equal(0, (await Json(await api.GetAsync("/api/candidates/search?name=zzzz-nobody"))).GetArrayLength());
    }

    [Fact]
    public async Task Register_validates_input_and_rejects_duplicates()
    {
        using var api = factory.Api();
        var email = NewEmail();
        var ok = await api.PostAsJsonAsync("/api/auth/register", new { name = "Jane Doe", email, password = "simple" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var body = await Json(ok);
        AssertKeys(body, "message");
        Assert.Equal("Registration successful", body.GetProperty("message").GetString());

        var duplicate = await api.PostAsJsonAsync("/api/auth/register", new { name = "Jane Again", email = email.ToUpperInvariant(), password = Password });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("already exists", (await Json(duplicate)).GetProperty("message").GetString());

        foreach (var (payload, expected) in new (object, string)[] {
            (new { name = "Short", email = NewEmail(), password = "12345" }, "at least 6"),
            (new { name = "Bad Email", email = "not-an-email", password = Password }, "valid email"),
            (new { name = "", email = NewEmail(), password = Password }, "Name is required"),
            (new { email = NewEmail(), password = Password }, "Name is required"),
        })
        {
            var response = await api.PostAsJsonAsync("/api/auth/register", payload);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var error = await Json(response);
            AssertKeys(error, "message");
            Assert.Contains(expected, error.GetProperty("message").GetString());
        }
    }

    [Fact]
    public async Task Login_returns_token_name_and_email_or_401()
    {
        using var api = factory.Api();
        var email = NewEmail();
        await api.PostAsJsonAsync("/api/auth/register", new { name = "Jane Doe", email, password = Password });

        var login = await api.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await Json(login);
        AssertKeys(body, "token", "name", "email");
        Assert.Equal("Jane Doe", body.GetProperty("name").GetString());
        Assert.Equal(email, body.GetProperty("email").GetString());
        var token = new JsonWebToken(body.GetProperty("token").GetString());
        Assert.Equal("MemsApi", token.Issuer);
        Assert.Contains("MemsIosApp", token.Audiences);
        Assert.Equal(email, token.GetClaim("email").Value);
        Assert.True(token.ValidTo > DateTime.UtcNow.AddDays(1));

        foreach (var payload in new object[] { new { email, password = "wrong-password" }, new { email = NewEmail(), password = Password } })
        {
            var failed = await api.PostAsJsonAsync("/api/auth/login", payload);
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
            Assert.Equal("Invalid email or password", (await Json(failed)).GetProperty("message").GetString());
        }
    }

    [Fact]
    public async Task Saved_candidates_require_a_valid_jwt()
    {
        using var api = factory.Api();
        var anonymous = await api.GetAsync("/api/savedcandidates");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Contains("Authentication required", (await Json(anonymous)).GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.PostAsJsonAsync("/api/savedcandidates", new { candidateId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.DeleteAsync("/api/savedcandidates/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.SendAsync(Authorized(HttpMethod.Get, "/api/savedcandidates", "not-a-jwt"))).StatusCode);

        // A structurally valid token signed with a different key must be rejected.
        var forged = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor {
            Issuer = "MemsApi", Audience = "MemsIosApp", Expires = DateTime.UtcNow.AddHours(1),
            Claims = new Dictionary<string, object> { ["sub"] = "someone" },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('x', 64))), SecurityAlgorithms.HmacSha256)
        });
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.SendAsync(Authorized(HttpMethod.Get, "/api/savedcandidates", forged))).StatusCode);

        var (token, _) = await RegisterAndLogin(api);
        Assert.Equal(HttpStatusCode.OK, (await api.SendAsync(Authorized(HttpMethod.Get, "/api/savedcandidates", token))).StatusCode);
    }

    [Fact]
    public async Task Saved_candidates_crud_is_scoped_to_the_signed_in_user()
    {
        using var api = factory.Api();
        var (token, _) = await RegisterAndLogin(api, "Voter One");
        var (otherToken, _) = await RegisterAndLogin(api, "Voter Two");
        var candidates = (await Json(await api.GetAsync("/api/candidates"))).EnumerateArray().Select(x => x.GetProperty("candidateId").GetInt32()).ToList();
        int first = candidates[0], second = candidates[1];

        Assert.Equal(0, (await Json(await api.SendAsync(Authorized(HttpMethod.Get, "/api/savedcandidates", token)))).GetArrayLength());

        var created = await api.SendAsync(Authorized(HttpMethod.Post, "/api/savedcandidates", token, new { candidateId = first }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var saved = await Json(created);
        AssertKeys(saved, "savedCandidateId", "userId", "candidateId", "dateSaved", "candidate");
        AssertKeys(saved.GetProperty("candidate"), CandidateKeys);
        Assert.Equal(first, saved.GetProperty("candidateId").GetInt32());
        Assert.Equal(first, saved.GetProperty("candidate").GetProperty("candidateId").GetInt32());
        Assert.StartsWith("https://", saved.GetProperty("candidate").GetProperty("photoUrl").GetString());
        Assert.False(string.IsNullOrEmpty(saved.GetProperty("userId").GetString()));
        Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ$", saved.GetProperty("dateSaved").GetString());
        var savedId = saved.GetProperty("savedCandidateId").GetInt32();

        var duplicate = await api.SendAsync(Authorized(HttpMethod.Post, "/api/savedcandidates", token, new { candidateId = first }));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("Candidate already in voting guide", (await Json(duplicate)).GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await api.SendAsync(Authorized(HttpMethod.Post, "/api/savedcandidates", token, new { candidateId = 99999 }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.SendAsync(Authorized(HttpMethod.Post, "/api/savedcandidates", token, new { }))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await api.SendAsync(Authorized(HttpMethod.Post, "/api/savedcandidates", token, new { candidateId = second }))).StatusCode);

        var mine = (await Json(await api.SendAsync(Authorized(HttpMethod.Get, "/api/savedcandidates", token)))).EnumerateArray().ToList();
        Assert.Equal(new[] { first, second }.Order(), mine.Select(x => x.GetProperty("candidateId").GetInt32()).Order());
        // Another user cannot see or delete these entries.
        Assert.Equal(0, (await Json(await api.SendAsync(Authorized(HttpMethod.Get, "/api/savedcandidates", otherToken)))).GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await api.SendAsync(Authorized(HttpMethod.Delete, $"/api/savedcandidates/{savedId}", otherToken))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await api.SendAsync(Authorized(HttpMethod.Post, "/api/savedcandidates", otherToken, new { candidateId = first }))).StatusCode);

        // Delete by SavedCandidateId, then by CandidateId fallback.
        Assert.Equal(HttpStatusCode.NoContent, (await api.SendAsync(Authorized(HttpMethod.Delete, $"/api/savedcandidates/{savedId}", token))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await api.SendAsync(Authorized(HttpMethod.Delete, $"/api/savedcandidates/{second}", token))).StatusCode);
        Assert.Equal(0, (await Json(await api.SendAsync(Authorized(HttpMethod.Get, "/api/savedcandidates", token)))).GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await api.SendAsync(Authorized(HttpMethod.Delete, $"/api/savedcandidates/{second}", token))).StatusCode);
        Assert.Equal(1, (await Json(await api.SendAsync(Authorized(HttpMethod.Get, "/api/savedcandidates", otherToken)))).GetArrayLength());
    }

    [Fact]
    public async Task Swagger_cors_and_root_redirect_are_configured()
    {
        using var api = factory.Api();
        var swagger = await api.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        var paths = (await Json(swagger)).GetProperty("paths");
        foreach (var path in new[] { "/api/auth/register", "/api/auth/login", "/api/municipalities", "/api/positions/{id}", "/api/candidates/search", "/api/savedcandidates/{id}" })
            Assert.True(paths.TryGetProperty(path, out _), path);
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync("/swagger/index.html")).StatusCode);

        var root = await api.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, root.StatusCode);
        Assert.Equal("/swagger", root.Headers.Location!.ToString());

        var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/savedcandidates");
        preflight.Headers.Add("Origin", "https://example.com");
        preflight.Headers.Add("Access-Control-Request-Method", "DELETE");
        preflight.Headers.Add("Access-Control-Request-Headers", "authorization");
        var cors = await api.SendAsync(preflight);
        Assert.Equal(HttpStatusCode.NoContent, cors.StatusCode);
        Assert.Equal("*", cors.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Forwarded_https_scheme_is_used_for_image_urls()
    {
        // Simulates Azure App Service, which terminates TLS and forwards plain HTTP with X-Forwarded-Proto.
        using var api = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://mems.example.net") });
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/municipalities");
        request.Headers.Add("X-Forwarded-Proto", "https");
        var first = (await Json(await api.SendAsync(request))).EnumerateArray().First();
        Assert.StartsWith("https://mems.example.net/images/logos/", first.GetProperty("logoUrl").GetString());
    }
}
