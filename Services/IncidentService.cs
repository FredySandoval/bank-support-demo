using BankSupportDemo.Data;
using BankSupportDemo.Models;

namespace BankSupportDemo.Services;

public class IncidentService(AppDbContext db, ILogger<IncidentService> logger)
{
    public async Task RecordFailureAsync(string message, int? transferId = null)
    {
        try
        {
            db.Incidents.Add(new Incident
            {
                Type = "TransferFailure", Message = message, TransferId = transferId
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // A database outage can prevent incident persistence; stdout remains the fallback.
            logger.LogError(ex, "Could not persist transfer incident: {IncidentMessage}", message);
            db.ChangeTracker.Clear();
        }
    }
}
