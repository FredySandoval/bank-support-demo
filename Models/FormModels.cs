using System.ComponentModel.DataAnnotations;

namespace BankSupportDemo.Models;

public class LoginForm
{
    [Required]
    public string Username { get; set; } = "";
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";
}

public class TransferForm
{
    [Required]
    public int? SourceAccountId { get; set; }
    [Required]
    public int? DestinationAccountId { get; set; }
    [Required]
    public decimal? Amount { get; set; }
    public List<Account> Accounts { get; set; } = [];
}
