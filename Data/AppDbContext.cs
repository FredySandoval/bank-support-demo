using BankSupportDemo.Models;
using Microsoft.EntityFrameworkCore;

namespace BankSupportDemo.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<Incident> Incidents => Set<Incident>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<User>().HasIndex(u => u.Username).IsUnique();
        // Store money as integer cents, avoiding SQLite floating-point arithmetic.
        model.Entity<Account>().Property(a => a.Balance)
            .HasConversion(value => (long)(value * 100m), value => value / 100m);
        model.Entity<Transfer>().Property(t => t.Amount)
            .HasConversion(value => (long)(value * 100m), value => value / 100m);
        model.Entity<Transfer>().HasOne<Account>().WithMany()
            .HasForeignKey(t => t.SourceAccountId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Transfer>().HasOne<Account>().WithMany()
            .HasForeignKey(t => t.DestinationAccountId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Incident>().HasOne<Transfer>().WithMany()
            .HasForeignKey(i => i.TransferId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Transfer>().ToTable(t =>
            t.HasCheckConstraint("CK_Transfer_Status", "Status IN ('Completed', 'Failed')"));
    }
}
