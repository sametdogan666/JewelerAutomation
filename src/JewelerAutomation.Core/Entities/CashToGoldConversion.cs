namespace JewelerAutomation.Core.Entities;

public class CashToGoldConversion : BaseEntity
{
    public DateTime TransactionDate { get; set; }
    public decimal CashAmount { get; set; }
    public decimal HasPrice { get; set; }
    public decimal ConvertedGoldHas { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Description { get; set; }
    
    public Customer? Customer { get; set; }
}
