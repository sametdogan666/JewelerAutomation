namespace JewelerAutomation.Core.Entities;

/// <summary>
/// Satış kaynaklı, nakit bağlama (FIFO) ile eşleşmeyi bekleyen has gram pozisyonu.
/// Her satış kalemi (veya eski tek-kalem satış) için bir satır.
/// </summary>
public class GoldTransaction : BaseEntity
{
    /// <summary>Ana sepet/fatura (Transaction).</summary>
    public Guid TransactionId { get; set; }
    public Transaction Transaction { get; set; } = null!;

    /// <summary>Sepet satış kalemi; tek-kalem eski kayıtlarda null.</summary>
    public Guid? TransactionItemId { get; set; }
    public TransactionItem? TransactionItem { get; set; }

    /// <summary>O satışta açığa çıkan has gram (orijinal).</summary>
    public decimal OriginalHasGram { get; set; }

    /// <summary>FIFO bağlantılar sonrası kalan eşleşmemiş has gram.</summary>
    public decimal RemainingGram { get; set; }

    public bool IsFullyLinked { get; set; }
}
