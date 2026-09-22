using BankSupportDemo.Data;
using BankSupportDemo.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BankSupportDemo.Services;

public class AuthenticationService(AppDbContext db, PasswordHasher<User> hasher)
{
    public async Task<User?> ValidateAsync(string username, string password)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Username == username);
        if (user is null) return null;
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed) return null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, password);
            await db.SaveChangesAsync();
        }
        return user;
    }
}
