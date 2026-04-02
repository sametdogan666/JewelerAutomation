namespace JewelerAutomation.Core.Entities;

public enum LedgerEntryType
{
    GoldIn,
    GoldOut,
    CashIn,
    CashOut
}

public enum LedgerReferenceType
{
    Transaction,
    CustomerTransaction,
    SafeMovement,
    CustomerMovement,
    CashPegging,
    CashToGoldConversion,
    ManualAdjustment,
    /// <summary>FIFO nakit bağlama işlemi (kasadan nakit çıkış + altın giriş).</summary>
    LinkingProcess
}

public class LedgerEntry : BaseEntity
{
    public DateTime TransactionDate { get; set; }
    public LedgerEntryType EntryType { get; set; }
    public decimal GoldHasAmount { get; set; }
    public decimal CashAmount { get; set; }
    public LedgerReferenceType ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// Links this ledger entry to a parent operation (e.g. CashPegging).
    /// </summary>
    public Guid? CorrelationId { get; set; }
    
    public Customer? Customer { get; set; }
}
