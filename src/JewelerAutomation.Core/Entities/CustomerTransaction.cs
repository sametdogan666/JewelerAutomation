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
    /// <summary> Nakit tutarı (TL) - decimal(18,6) </summary>
    public decimal CashAmount { get; set; }
    public string? Description { get; set; }
}

public enum CustomerTransactionType
{
    GoldPurchase = 0,   // Müşteri altın aldı (cariye altın çıkışı)
    GoldSale = 1,       // Müşteri altın sattı (cariye altın girişi)
    CashPayment = 2,    // Müşteri nakit ödedi (borç azalır)
    CashCollection = 3 // Nakit tahsilat / müşteriye ödeme (borç artar / alacak azalır)
}
