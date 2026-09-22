using System.Security.Claims;
using BankSupportDemo.Models;
using BankSupportDemo.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuthenticationService = BankSupportDemo.Services.AuthenticationService;

namespace BankSupportDemo.Controllers;

[Authorize]
public class AuthController(AuthenticationService authentication, ILogger<AuthController> logger) : Controller
{
    [AllowAnonymous, HttpGet("/login")]
    public IActionResult Login() => View(new LoginForm());

    [AllowAnonymous, HttpPost("/login")]
    public async Task<IActionResult> Login(LoginForm form)
    {
        if (!ModelState.IsValid) return View(form);
        var user = await authentication.ValidateAsync(form.Username, form.Password);
        if (user is null)
        {
            ModelState.AddModelError("", "Invalid username or password.");
            return View(form);
        }
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username)
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        logger.LogInformation("User {UserId} logged in", user.Id);
        return Redirect("/accounts");
    }

    [HttpPost("/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/login");
    }
}
