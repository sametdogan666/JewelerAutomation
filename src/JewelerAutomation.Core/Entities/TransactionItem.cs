namespace JewelerAutomation.Core.Entities;

/// <summary>
/// Sepet/fatura satırı — her bir alış veya satış kalemi.
/// Aynı sepette hem alış hem satış satırları olabilir.
/// </summary>
public class TransactionItem : BaseEntity
{
    public Guid TransactionId { get; set; }
    public Transaction? Transaction { get; set; }

    public TransactionDirection Direction { get; set; }

    /// <summary> Gram (Miktar veya Ağırlık) - decimal(18,6) </summary>
    public decimal Quantity { get; set; }
    /// <summary> Saflık (916, 995 vb.) - decimal(18,6) </summary>
    public decimal Milyem { get; set; }
    public int? PieceCount { get; set; }
    public decimal? UnitLabour { get; set; }
    /// <summary> ±(Adet * Birimİşçilik * 0.01) - decimal(18,6) </summary>
    public decimal TotalLabour { get; set; }
    /// <summary> Has Gram - decimal(18,6) </summary>
    public decimal HasGram { get; set; }
    /// <summary> Toplam tutar (TL) - decimal(18,6) </summary>
    public decimal? Price { get; set; }
    public string? Description { get; set; }
    /// <summary> Milyem > 916 için (Milyem-916)*Miktar*0.001 - decimal(18,6) </summary>
    public decimal MilyemLabour { get; set; }
}
