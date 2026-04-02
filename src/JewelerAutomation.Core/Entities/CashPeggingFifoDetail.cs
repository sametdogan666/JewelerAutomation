namespace JewelerAutomation.Core.Entities;

/// <summary>
/// Hibrit dönem nakit bağlamada FIFO satış pozisyonundan düşülen gram; silmede geri yüklenir.
/// </summary>
public class CashPeggingFifoDetail : BaseEntity
{
    public Guid CashPeggingLogId { get; set; }
    public CashPeggingLog CashPeggingLog { get; set; } = null!;

    public Guid GoldTransactionId { get; set; }
    public GoldTransaction GoldTransaction { get; set; } = null!;

    public decimal AmountDeducted { get; set; }
}
