namespace JewelerAutomation.Core.Entities;

/// <summary>
/// Nakit Bağlama işleminin kaydı.
/// Kullanıcı biriken nakiti belirli bir has fiyatından altına çevirdiğinde oluşur.
/// </summary>
public class CashPeggingLog : ISoftDelete
{
    public Guid Id { get; set; }

    /// <summary>
    /// Groups all related records (Transaction, SafeMovement, LedgerEntry) created by this pegging operation.
    /// </summary>
    public Guid CorrelationId { get; set; }
    
    /// <summary>
    /// Nakit bağlama işleminin yapıldığı tarih.
    /// </summary>
    public DateTime PeggingDate { get; set; }
    
    /// <summary>
    /// Bağlanan nakit miktarı (TL).
    /// </summary>
    public decimal CashAmount { get; set; }
    
    /// <summary>
    /// Bağlama yapılan has altın fiyatı (TL/gram).
    /// </summary>
    public decimal GoldPricePerGram { get; set; }
    
    /// <summary>
    /// Nakit karşılığı has gram = CashAmount / GoldPricePerGram.
    /// </summary>
    public decimal EquivalentHasGram { get; set; }
    
    /// <summary>
    /// O andaki fiziksel kasa altın stoku (Has Gr).
    /// </summary>
    public decimal PhysicalGoldAtTime { get; set; }
    
    /// <summary>
    /// Bağlama sonrası toplam sermaye (Has Gr cinsinden).
    /// = (CashAmount / GoldPrice) + PhysicalGold
    /// </summary>
    public decimal TotalCapitalHasGram { get; set; }
    
    /// <summary>
    /// Dönem başlangıç tarihi (kâr hesaplaması için).
    /// </summary>
    public DateTime PeriodStartDate { get; set; }
    
    /// <summary>
    /// Dönem bitiş tarihi (genelde bağlama tarihi).
    /// </summary>
    public DateTime PeriodEndDate { get; set; }
    
    /// <summary>
    /// Bu dönemdeki işlem kârı (Has Gr).
    /// Sadece alış-satış marjından gelen kâr.
    /// </summary>
    public decimal TransactionProfitHasGram { get; set; }
    
    /// <summary>
    /// Bu dönemdeki kur farkı kârı (Has Gr).
    /// Altın fiyat artışından kaynaklanan kâr.
    /// </summary>
    public decimal ExchangeRateProfitHasGram { get; set; }
    
    /// <summary>
    /// Net kâr = Transaction Profit + Exchange Rate Profit.
    /// </summary>
    public decimal NetProfitHasGram { get; set; }
    
    /// <summary>
    /// Açıklama/notlar.
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Bağlamayı yapan kullanıcı ID (opsiyonel).
    /// </summary>
    public Guid? UserId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
