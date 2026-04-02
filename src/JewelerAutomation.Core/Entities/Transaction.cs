namespace JewelerAutomation.Core.Entities;

/// <summary>
/// Sepet/Fatura başlığı — birden fazla alış-satış kalemini tek işlemde gruplar.
/// Eski tek-kalemli kayıtlarla geriye uyumluluğu korur.
/// </summary>
public class Transaction : BaseEntity
{
    public DateTime TransactionDate { get; set; }

    /// <summary>
    /// Net yön: Sepetteki toplam altın pozitifse Purchase, negatifse Sale.
    /// Eski tek-kalemli kayıtlarda orijinal yön korunur.
    /// </summary>
    public TransactionDirection Direction { get; set; }

    /// <summary> Eski tek-kalem alanı — yeni sepet kayıtlarında 0 </summary>
    public decimal Quantity { get; set; }
    /// <summary> Eski tek-kalem alanı — yeni sepet kayıtlarında 0 </summary>
    public decimal Milyem { get; set; }
    public int? PieceCount { get; set; }
    public decimal? UnitLabour { get; set; }
    public decimal TotalLabour { get; set; }

    /// <summary> Net mutlak Has Gram (geriye uyumluluk) - decimal(18,6) </summary>
    public decimal HasGram { get; set; }
    /// <summary> Net mutlak tutar (geriye uyumluluk) - decimal(18,6) </summary>
    public decimal? Price { get; set; }
    public string? Description { get; set; }
    public decimal MilyemLabour { get; set; }

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>
    /// Links this transaction to a parent operation (e.g. CashPegging).
    /// </summary>
    public Guid? CorrelationId { get; set; }

    // ── Basket / Invoice fields ──

    /// <summary>
    /// İşaretli net Has Gram: Pozitif = kasaya altın giriş, Negatif = kasadan altın çıkış.
    /// </summary>
    public decimal NetHasGram { get; set; }

    /// <summary>
    /// İşaretli net nakit: Pozitif = kasaya nakit giriş, Negatif = kasadan nakit çıkış.
    /// </summary>
    public decimal NetCashAmount { get; set; }

    /// <summary>
    /// Nakit bağlama işleminde bağlanan toplam nakit (TL), pozitif tutar. Diğer kayıtlarda null.
    /// </summary>
    public decimal? CashAmount { get; set; }

    /// <summary>
    /// Nakit bağlama işleminde elde edilen has gram karşılığı, pozitif. Diğer kayıtlarda null.
    /// </summary>
    public decimal? EquivalentHasGram { get; set; }

    /// <summary>
    /// Sepetteki kalemler (master-detail).
    /// </summary>
    public ICollection<TransactionItem> Items { get; set; } = new List<TransactionItem>();
}

public enum TransactionDirection
{
    Sale = 0,
    Purchase = 1
}
