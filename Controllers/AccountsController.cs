using BankSupportDemo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankSupportDemo.Controllers;

[Authorize]
public class AccountsController(AppDbContext db) : Controller
{
    [HttpGet("/accounts")]
    public async Task<IActionResult> Index() =>
        View(await db.Accounts.AsNoTracking().OrderBy(a => a.Id).ToListAsync());
}
