using BankSupportDemo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankSupportDemo.Controllers;

[Authorize]
public class IncidentsController(AppDbContext db) : Controller
{
    [HttpGet("/incidents")]
    public async Task<IActionResult> Index() =>
        View(await db.Incidents.AsNoTracking().OrderByDescending(i => i.Id).ToListAsync());
}
