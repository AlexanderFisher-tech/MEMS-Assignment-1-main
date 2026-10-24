/*
 * Project: Municipal Elections Management System (MEMS)
 * Course: COMP4976
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Configures MVC, Identity, storage, session state, and the request pipeline.
 */
using System.Threading.RateLimiting;
using Mems.Web.Data;
using Mems.Web.Models;
using Mems.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
// Console logging works in App Service and without Windows Event Log privileges.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
var storageRoot = builder.Configuration["Storage:Root"] ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(storageRoot);
// Persist cookie-protection keys alongside durable application storage so app restarts can reuse them.
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(storageRoot, "keys"))).SetApplicationName("MEMS");
var connection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection. Run scripts/setup.ps1 for Development.");
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
builder.Services.AddIdentity<IdentityUser, ApplicationRole>(options => {
    options.Password.RequiredLength = 8;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options => {
    options.LoginPath = "/Account/Login"; options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true; options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
// The admin policy grants area entry; AdminScope separately enforces municipality ownership.
builder.Services.AddAuthorization(options => options.AddPolicy("Admin", policy => policy.RequireRole(AdminScope.SuperRole, AdminScope.MunicipalRole)));
// Apply anti-forgery validation globally to modifying MVC requests, including public guide actions.
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AdminScope>();
builder.Services.AddScoped<VotingSelection>();
builder.Services.AddSingleton<ImageStore>();
// Session choices use process memory and therefore expire on an app restart.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromHours(8); options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; options.Cookie.SameSite = SameSiteMode.Lax;
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options => options.MultipartBodyLengthLimit = 3 * 1024 * 1024);
builder.Services.AddRateLimiter(options => {
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); app.UseHttpsRedirection(); }
app.Use(async (context, next) => {
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
// Session and authentication must run before controllers consume selections or authorize admin access.
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("areas", "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute("default", "{controller=Municipalities}/{action=Index}/{id?}");
using (var scope = app.Services.CreateScope())
    await SeedData.Initialize(scope.ServiceProvider, app.Environment.IsDevelopment(), app.Configuration);
app.Run();
public partial class Program { }
