namespace JewelerAutomation.Core.Entities;

/// <summary>
/// Bir bağlama işleminde hangi GoldTransaction kaydından ne kadar düşüldüğü.
/// </summary>
public class LinkingDetail : BaseEntity
{
    public Guid LinkingProcessId { get; set; }
    public LinkingProcess LinkingProcess { get; set; } = null!;

    public Guid GoldTransactionId { get; set; }
    public GoldTransaction GoldTransaction { get; set; } = null!;

    public decimal AmountDeducted { get; set; }
}
