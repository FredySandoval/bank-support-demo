namespace BankSupportDemo.Models;

public class Incident
{
    public int Id { get; set; }
    public int? TransferId { get; set; }
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
