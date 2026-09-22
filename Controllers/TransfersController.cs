using BankSupportDemo.Data;
using BankSupportDemo.Models;
using BankSupportDemo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankSupportDemo.Controllers;

[Authorize]
public class TransfersController(AppDbContext db, TransferService transfers) : Controller
{
    [HttpGet("/transfers/new")]
    public async Task<IActionResult> New() => View(await PopulateAsync(new TransferForm()));

    [HttpPost("/transfers/new")]
    public async Task<IActionResult> New(TransferForm form)
    {
        if (ModelState.IsValid)
        {
            var error = await transfers.ExecuteAsync(form.SourceAccountId!.Value,
                form.DestinationAccountId!.Value, form.Amount!.Value);
            if (error is null)
            {
                TempData["Success"] = "Transfer completed.";
                return Redirect("/accounts");
            }
            ModelState.AddModelError("", error);
        }
        return View(await PopulateAsync(form));
    }

    private async Task<TransferForm> PopulateAsync(TransferForm form)
    {
        form.Accounts = await db.Accounts.AsNoTracking().OrderBy(a => a.Id).ToListAsync();
        return form;
    }
}
