namespace BankSupportDemo.Models;

public class Account
{
    public int Id { get; set; }
    public string Owner { get; set; } = "";
    public decimal Balance { get; set; }
}
