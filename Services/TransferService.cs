using BankSupportDemo.Data;
using BankSupportDemo.Models;

namespace BankSupportDemo.Services;

public class TransferService(AppDbContext db, IncidentService incidents, ILogger<TransferService> logger)
{
    // Fits safely in SQLite integer cents; keep this demo's input range explicit.
    public const decimal MaximumAmount = 1_000_000_000m;

    public async Task<string?> ExecuteAsync(int sourceId, int destinationId, decimal amount)
    {
        logger.LogInformation("Transfer requested from account {SourceId} to account {DestinationId} for {Amount}",
            sourceId, destinationId, amount);
        string? rejection = null;
        try
        {
            // SQLite's immediate write transaction serializes writers before balances are read.
            // Disposal rolls back any uncommitted transaction, including on validation failure.
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                var source = await db.Accounts.FindAsync(sourceId);
                var destination = await db.Accounts.FindAsync(destinationId);
                rejection = source is null ? "Invalid source account."
                    : destination is null ? "Invalid destination account."
                    : sourceId == destinationId ? "Source and destination must be different."
                    : amount <= 0 || amount > MaximumAmount ? "Amount must be greater than zero and at most 1,000,000,000."
                    : decimal.Round(amount, 2) != amount ? "Amount must have at most two decimal places."
                    : source.Balance < amount ? "Insufficient funds"
                    : destination.Balance > MaximumAmount - amount ? "Destination balance limit exceeded."
                    : null;

                if (rejection is null)
                {
                    source!.Balance -= amount;
                    destination!.Balance += amount;
                    var transfer = new Transfer
                    {
                        SourceAccountId = sourceId, DestinationAccountId = destinationId,
                        Amount = amount, Status = "Completed"
                    };
                    db.Transfers.Add(transfer);
                    await db.SaveChangesAsync();
                    await transaction.CommitAsync();
                    logger.LogInformation("Transfer {TransferId} completed", transfer.Id);
                    return null;
                }
                await transaction.RollbackAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected transfer failure from {SourceId} to {DestinationId}", sourceId, destinationId);
            db.ChangeTracker.Clear();
            await incidents.RecordFailureAsync("Unexpected transfer error. Check application logs before retrying.");
            return "Transfer could not be confirmed. Check balances or contact support before retrying.";
        }

        db.ChangeTracker.Clear();
        logger.LogWarning("Transfer rejected from {SourceId} to {DestinationId}: {Reason}",
            sourceId, destinationId, rejection);
        await incidents.RecordFailureAsync(rejection!);
        return rejection;
    }
}
