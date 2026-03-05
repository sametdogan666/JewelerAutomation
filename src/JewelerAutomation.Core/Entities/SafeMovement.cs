namespace JewelerAutomation.Core.Entities;

/// <summary>
/// Kasa hareketi - Excel Kasa: TARİH, GRAM, MİLYEM, HAS-GR, AÇIKLAMA.
/// HasGram = Gram * Milyem / 1000.
/// Ana sermaye (toplam stok) = SUM(HasGram) tüm hareketler.
/// </summary>
public class SafeMovement : BaseEntity
{
    public DateTime TransactionDate { get; set; }
    /// <summary> Ham gram - decimal(18,6) </summary>
    public decimal Gram { get; set; }
    /// <summary> Saflık (1000 = has) - decimal(18,6) </summary>
    public decimal Milyem { get; set; }
    /// <summary> Has Gram = Gram * Milyem / 1000 - decimal(18,6) </summary>
    public decimal HasGram { get; set; }
    public string? Description { get; set; }
    /// <summary> Gelir / Gider / Ana sermaye girişi vb. </summary>
    public SafeMovementType MovementType { get; set; }
    public Guid? SourceTransactionId { get; set; }
}

public enum SafeMovementType
{
    Income = 0,
    Expense = 1,
    Capital = 2,
    Transfer = 3
}
