/*
 * Project: Municipal Elections Companion API (MEMS)
 * Course: COMP4977
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Configures EF Core/SQLite, Identity, JWT bearer auth, CORS, Swagger, static images, and startup seeding.
 */
using Mems.Api.Data;
using Mems.Api.Models;
using Mems.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Azure App Service keeps /home across restarts and redeployments; locally the database lives in App_Data.
var azureHome = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") is not null ? Environment.GetEnvironmentVariable("HOME") : null;
var storageRoot = builder.Configuration["Storage:Root"]
    ?? (azureHome is not null ? Path.Combine(azureHome, "data", "mems-api") : Path.Combine(builder.Environment.ContentRootPath, "App_Data"));
Directory.CreateDirectory(storageRoot);
var connection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? $"Data Source={Path.Combine(storageRoot, "mems-api.db")};Default Timeout=30";
builder.Services.AddDbContext<ApiDbContext>(options => options.UseSqlite(connection));

// Identity stores users only; JWT bearer (not cookies) authenticates API requests.
builder.Services.AddIdentityCore<ApplicationUser>(options => {
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireDigit = false;
}).AddEntityFrameworkStores<ApiDbContext>();

var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
var signingKey = jwt.SigningKey();
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<TokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => {
    // Keep standard JWT claim names (sub, email, name) instead of mapping them to long URI claim types.
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters {
        ValidateIssuer = true, ValidIssuer = jwt.Issuer,
        ValidateAudience = true, ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = signingKey,
        ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = JwtRegisteredClaimNames.Name,
    };
    // Give the iOS client the same { message } error shape on 401 as every other endpoint.
    options.Events = new JwtBearerEvents {
        OnChallenge = async context => {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsJsonAsync(new MessageResponse("Authentication required. Send a valid bearer token."));
        }
    };
});
builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<DtoMapper>();
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
    // Validation failures return { "message": "..." } rather than ProblemDetails.
    options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(new MessageResponse(string.Join(" ",
        context.ModelState.Values.SelectMany(x => x.Errors).Select(x => string.IsNullOrEmpty(x.ErrorMessage) ? "The request body is invalid." : x.ErrorMessage).Distinct()))));
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
// App Service terminates TLS at its front end; trust its forwarded scheme so image URLs are generated as https.
builder.Services.Configure<ForwardedHeadersOptions>(options => {
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => {
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Municipal Elections Companion API", Version = "v1",
        Description = "COMP4977 Web API. Log in via POST /api/auth/login, then click Authorize and paste the token." });
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Mems.Api.xml"));
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header,
        Description = "Paste the token returned by /api/auth/login (without the 'Bearer ' prefix)."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] });
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UseExceptionHandler(error => error.Run(context =>
    context.Response.WriteAsJsonAsync(new MessageResponse("An unexpected server error occurred."))));
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI(options => {
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "MEMS API v1");
    options.DocumentTitle = "Municipal Elections Companion API";
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapControllers();

// Zero-config deployment: create/upgrade the SQLite schema and seed sample data before serving requests.
using (var scope = app.Services.CreateScope())
    await SeedData.InitializeAsync(scope.ServiceProvider);

app.Run();

public partial class Program { }
