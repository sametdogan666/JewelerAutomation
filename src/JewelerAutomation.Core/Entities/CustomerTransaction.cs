namespace JewelerAutomation.Core.Entities;

/// <summary>
/// Cari hesap hareketi: altın alış/satış, nakit ödeme/tahsilat.
/// Bakiye: GoldHas toplamı (GoldPurchase +, GoldSale -), CashAmount toplamı (CashCollection +, CashPayment -).
/// </summary>
public class CustomerTransaction : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public DateTime TransactionDate { get; set; }
    public CustomerTransactionType TransactionType { get; set; }

    /// <summary> Altın miktarı (gram) - decimal(18,6) </summary>
    public decimal GoldGram { get; set; }
    /// <summary> Saflık (milyem) - decimal(18,6) </summary>
    public decimal GoldMilyem { get; set; }
    /// <summary> Has gram = GoldGram * GoldMilyem / 1000 - decimal(18,6) </summary>
    public decimal GoldHas { get; set; }
    /// <summary> Nakit tutarı - decimal(18,6); para birimi <see cref="CashCurrency"/>. </summary>
    public decimal CashAmount { get; set; }

    /// <summary>Nakit satırlarında para birimi; altın satırlarında genelde TRY.</summary>
    public CashCurrency CashCurrency { get; set; } = CashCurrency.Try;

    /// <summary>False: yalnızca cari defteri (CustomerTransaction); kasa/deftere yazılmaz (devir, emanet yükümlülüğü).</summary>
    public bool PostToLedger { get; set; } = true;

    /// <summary>Sepetten oluşan emanet yükümlülüğü satırlarında dolu.</summary>
    public Guid? SourceBasketTransactionId { get; set; }
    public Transaction? SourceBasketTransaction { get; set; }

    /// <summary>Yalnız <see cref="CustomerTransactionType.OpeningBalance"/>.</summary>
    public SahisOpeningAssetKind? OpeningAssetKind { get; set; }

    /// <summary>Yalnız açılış: true = müşteri alacaklı (biz borçluyuz).</summary>
    public bool? OpeningCustomerIsCreditor { get; set; }

    public string? Description { get; set; }
}

public enum CustomerTransactionType
{
    GoldPurchase = 0,   // Müşteri altın aldı (cariye altın çıkışı)
    GoldSale = 1,       // Müşteri altın sattı (cariye altın girişi)
    CashPayment = 2,    // Müşteri nakit ödedi (borç azalır)
    CashCollection = 3, // Nakit tahsilat / müşteriye ödeme (borç artar / alacak azalır)

    /// <summary>Şahıs devir — kasa/kasa defterine yazılmaz; <see cref="OpeningAssetKind"/> kullanılır.</summary>
    OpeningBalance = 20,

    /// <summary>Şahıs emanet sepetinden oluşan altın yükümlülüğü; deftere yazılmaz.</summary>
    SahisEmanetLiability = 21
}
