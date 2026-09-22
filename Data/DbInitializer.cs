using BankSupportDemo.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BankSupportDemo.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var passwordFile = configuration["AdminPasswordFile"]
            ?? throw new InvalidOperationException("AdminPasswordFile must point to a password secret file.");
        var password = (await File.ReadAllTextAsync(passwordFile)).TrimEnd('\r', '\n');
        if (password.Length < 16)
            throw new InvalidOperationException("Admin password must contain at least 16 characters.");

        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var hasher = services.GetRequiredService<PasswordHasher<User>>();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Username == "admin");
        if (user is null)
        {
            user = new User { Username = "admin" };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Users.Add(user);
        }
        else if (hasher.VerifyHashedPassword(user, user.PasswordHash, password)
                 != PasswordVerificationResult.Success)
        {
            user.PasswordHash = hasher.HashPassword(user, password);
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
