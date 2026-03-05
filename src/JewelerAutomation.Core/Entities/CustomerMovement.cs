namespace JewelerAutomation.Core.Entities;

/// <summary>
/// Cari/Şahıs hareketi - Excel'deki tek satır: Miktar, Milyem, HAS-GR, DURUM (VERİLDİ/ALINDI).
/// Bakiye: SUM(HasGram) tüm hareketler üzerinden.
/// </summary>
public class CustomerMovement : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public DateTime TransactionDate { get; set; }
    /// <summary> Gram cinsinden miktar. Pozitif = biz veriyoruz, negatif = bize veriliyor. </summary>
    public decimal Quantity { get; set; }
    /// <summary> Saflık (916, 995, 1000 vb.) - decimal(18,6) </summary>
    public decimal Milyem { get; set; }
    public int? PieceCount { get; set; }
    public decimal? UnitLabour { get; set; }
    /// <summary> Toplam İşçilik = ±(Adet * Birimİşçilik * 0.01) </summary>
    public decimal TotalLabour { get; set; }
    /// <summary> Has Gram = (Miktar*Milyem/1000) + TotalLabour - decimal(18,6) </summary>
    public decimal HasGram { get; set; }
    public string? Description { get; set; }
    /// <summary> VERİLDİ / ALINDI - Miktar > 0 => Verildi, Miktar < 0 => Alındı </summary>
    public MovementDirection Direction { get; set; }
    /// <summary> Milyem > 916 için (Milyem-916)*Miktar*0.001 </summary>
    public decimal MilyemLabour { get; set; }
    public Guid? SourceTransactionId { get; set; }
}

public enum MovementDirection
{
    Empty = 0,
    Verildi = 1,  // Biz verdik
    Alindi = 2    // Biz aldık
}
