namespace JewelerAutomation.Application.Services;

/// <summary>
/// Excel'deki formüllere dayalı kuyumculuk hesaplamaları (Has Gram, Milyem, İşçilik).
/// Tüm sayısal sonuçlar decimal(18,6) hassasiyetinde tutulmalıdır.
/// </summary>
public interface IAccountingService
{
    /// <summary>
    /// Toplam İşçilik = ±(Adet * Birimİşçilik * 0.01). Varsayılan pozitif: Has = saf + işçilik.
    /// </summary>
    decimal CalculateTotalLabour(int pieceCount, decimal unitLabour, bool subtract = false);

    /// <summary>
    /// Has Gram (satış, işçilik dahil): saf has (gr×milyem, milyem ≤1 ondalık saflık; &gt;1 binlik ayar) + Toplamİşçilik.
    /// </summary>
    decimal CalculateHasGramWithLabour(decimal quantity, decimal milyem, decimal totalLabour);

    /// <summary>
    /// Has Gram (sade, işçiliksiz): milyem ≤ 1 ise gr×milyem (ör. 0,916); aksi halde gr×milyem×0,001 (ör. 916).
    /// </summary>
    decimal CalculateHasGram(decimal quantity, decimal milyem);

    /// <summary>
    /// Milyem İşçilik (916 üzeri fazlalık gram): Milyem > 916 ise (Milyem - 916) * Miktar * 0.001, değilse 0.
    /// Excel: J = IF(C>916, (C-916)*B*0.001, 0)
    /// </summary>
    decimal CalculateMilyemLabour(decimal quantity, decimal milyem, bool onlyWhenAlindi = false);

    /// <summary>
    /// Cari/Şahıs hareket yönü: Miktar > 0 => Verildi, Miktar < 0 => Alındı, HasGram = 0 => Boş.
    /// Excel: I = IF(G=0,"BOŞ", IF(B>0,"VERİLDİ","ALINDI"))
    /// </summary>
    Core.Entities.MovementDirection GetMovementDirection(decimal quantity, decimal hasGram);
    
    /// <summary>
    /// Kuyumculuk Kar/Zarar hesaplama (Nakit Bağlama Mantığı).
    /// Formula: Net Sermaye = (Kasadaki Nakit / Has Fiyatı) + Kasadaki Altın
    ///          Net Kar = Net Sermaye - Başlangıç Sermayesi
    /// </summary>
    Task<AccountingProfitResult> CalculateProfitAsync(
        decimal goldPricePerGram, 
        DateTime? startDate = null, 
        DateTime? endDate = null, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// İlk "Ana Sermaye" (Capital) kasa hareketini bulur ve başlangıç sermayesi olarak döndürür.
    /// </summary>
    Task<decimal> GetInitialCapitalAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Transaction'lardan toplam nakit bakiyesini hesaplar (Satış - Alış).
    /// </summary>
    Task<CashBalanceResult> GetCashBalanceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Belirli tarih aralığındaki tüm işlemleri getirir (detaylı analiz için).
    /// </summary>
    Task<PeriodTransactionSummary> GetPeriodTransactionSummaryAsync(
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Kuyumculuk kar/zarar sonucu (Nakit Bağlama Mantığı)
/// </summary>
public record AccountingProfitResult(
    decimal InitialCapitalHasGram,
    decimal CurrentGoldInSafeHasGram,
    decimal CurrentCashBalanceTL,
    decimal CashEquivalentHasGram,
    decimal NetCapitalHasGram,
    decimal NetProfitHasGram,
    decimal GoldPriceUsed
);

/// <summary>
/// Nakit bakiye detayı
/// </summary>
public record CashBalanceResult(
    decimal TotalSalesCash,
    decimal TotalPurchasesCash,
    decimal NetCashBalance
);

/// <summary>
/// Dönemsel işlem özeti.
/// </summary>
public record PeriodTransactionSummary(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    IReadOnlyList<TransactionDetail> Transactions,
    decimal TotalPurchasesHasGram,
    decimal TotalSalesHasGram,
    decimal TotalPurchasesCash,
    decimal TotalSalesCash,
    decimal NetCashChange,
    decimal NetGoldChange
);

/// <summary>
/// İşlem detayı (kâr analizi için).
/// </summary>
public record TransactionDetail(
    Guid Id,
    DateTime Date,
    string Direction,
    decimal Quantity,
    decimal Milyem,
    decimal HasGram,
    decimal Price,
    decimal CashImpact,
    string? CustomerName,
    string? Description
);
