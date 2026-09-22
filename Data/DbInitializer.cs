using BankSupportDemo.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BankSupportDemo.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!await db.Users.AnyAsync(u => u.Username == "admin"))
        {
            var user = new User { Username = "admin" };
            user.PasswordHash = services.GetRequiredService<PasswordHasher<User>>()
                .HashPassword(user, "Demo123!");
            db.Users.Add(user);
        }

        var seeds = new[]
        {
            new Account { Id = 1, Owner = "Alice Johnson", Balance = 5000m },
            new Account { Id = 2, Owner = "Bob Smith", Balance = 2500m },
            new Account { Id = 3, Owner = "Demo Company", Balance = 10000m }
        };
        foreach (var account in seeds)
            if (!await db.Accounts.AnyAsync(a => a.Id == account.Id))
                db.Accounts.Add(account);

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
