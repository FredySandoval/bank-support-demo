namespace BankSupportDemo.Models;

public class Transfer
{
    public int Id { get; set; }
    public int SourceAccountId { get; set; }
    public int DestinationAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Completed";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
