using BankSupportDemo.Data;
using BankSupportDemo.Models;
using BankSupportDemo.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<PasswordHasher<User>>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<TransferService>();
builder.Services.AddScoped<IncidentService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.Cookie.Name = "BankSupportDemo.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    });
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("sqlite");
Directory.CreateDirectory("data");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("data/keys"))
    .SetApplicationName("BankSupportDemo");

var app = builder.Build();
using (var scope = app.Services.CreateScope())
    await DbInitializer.InitializeAsync(scope.ServiceProvider);

// A generic response keeps database details out of browser error messages.
app.UseExceptionHandler(error => error.Run(async context =>
{
    context.Response.StatusCode = 500;
    context.Response.ContentType = "text/plain";
    await context.Response.WriteAsync("Something went wrong. Please try again or contact support.");
}));
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/accounts"));
app.Run();
