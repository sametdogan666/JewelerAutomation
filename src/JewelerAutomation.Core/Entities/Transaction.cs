namespace JewelerAutomation.Core.Entities;

/// <summary>
/// Alış-Satış kaydı (Excel: SATIŞ veya ALIŞ satırı).
/// SATIŞ: Miktar, Milyem, Adet, Birim İşçilik, HAS-GR, Fiyat, Açıklama.
/// ALIŞ: Ağırlık, Milyem, Has, Fiyat, Açıklama.
/// </summary>
public class Transaction : BaseEntity
{
    public DateTime TransactionDate { get; set; }
    public TransactionDirection Direction { get; set; } // Sale / Purchase

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
    /// <summary> Birim fiyat (TL/gram vb.) - decimal(18,6) </summary>
    public decimal? Price { get; set; }
    public string? Description { get; set; }
    /// <summary> Milyem > 916 için (Milyem-916)*Miktar*0.001 - decimal(18,6) </summary>
    public decimal MilyemLabour { get; set; }

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
}

public enum TransactionDirection
{
    Sale = 0,
    Purchase = 1
}
