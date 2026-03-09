using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Services;

/// <summary>
/// Excel formüllerine birebir uyumlu kuyumculuk hesaplama servisi.
/// Hassasiyet: decimal(18,6) - tüm çarpım/bölümler bu hassasiyette.
/// </summary>
public class AccountingService : IAccountingService
{
    private readonly IUnitOfWork _unitOfWork;
    
    private const decimal MilyemFactor = 0.001m;   // / 1000
    private const decimal LabourFactor = 0.01m;     // %1 birim işçilik çarpanı
    private const decimal MilyemThreshold = 916m;   // 916 üstü fazlalık hesabı
    
    public AccountingService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public decimal CalculateTotalLabour(int pieceCount, decimal unitLabour, bool subtract = true)
    {
        var labour = (decimal)pieceCount * unitLabour * LabourFactor;
        return subtract ? -labour : labour;
    }

    /// <inheritdoc />
    public decimal CalculateHasGramWithLabour(decimal quantity, decimal milyem, decimal totalLabour)
    {
        var hasFromPurity = quantity * milyem * MilyemFactor;
        return Math.Round(hasFromPurity + totalLabour, 6);
    }

    /// <inheritdoc />
    public decimal CalculateHasGram(decimal quantity, decimal milyem)
    {
        return Math.Round(quantity * milyem * MilyemFactor, 6);
    }

    /// <inheritdoc />
    public decimal CalculateMilyemLabour(decimal quantity, decimal milyem, bool onlyWhenAlindi = false)
    {
        if (milyem <= MilyemThreshold) return 0;
        return Math.Round((milyem - MilyemThreshold) * quantity * MilyemFactor, 6);
    }

    /// <inheritdoc />
    public MovementDirection GetMovementDirection(decimal quantity, decimal hasGram)
    {
        if (hasGram == 0) return MovementDirection.Empty;
        return quantity > 0 ? MovementDirection.Verildi : MovementDirection.Alindi;
    }
    
    /// <inheritdoc />
    public async Task<AccountingProfitResult> CalculateProfitAsync(
        decimal goldPricePerGram, 
        DateTime? startDate = null, 
        DateTime? endDate = null, 
        CancellationToken cancellationToken = default)
    {
        // 1. Başlangıç sermayesini al (ilk Ana Sermaye hareketi)
        var initialCapital = await GetInitialCapitalAsync(cancellationToken);
        
        // 2. Kasadaki toplam altın (Has Gram)
        var goldInSafe = await _unitOfWork.SafeMovements
            .GetTotalHasGramBalanceAsync(cancellationToken);
        
        // 3. Nakit bakiyesini al (Transaction'lardan)
        var cashBalance = await GetCashBalanceAsync(cancellationToken);
        var netCash = cashBalance.NetCashBalance;
        
        // 4. Nakit Bağlama: Nakit / Has Fiyatı
        var cashEquivalentGold = netCash / goldPricePerGram;
        
        // 5. Net Sermaye = Nakit Karşılığı + Altın
        var netCapital = cashEquivalentGold + goldInSafe;
        
        // 6. Net Kar/Zarar = Net Sermaye - Başlangıç Sermayesi
        var netProfit = netCapital - initialCapital;
        
        return new AccountingProfitResult(
            InitialCapitalHasGram: initialCapital,
            CurrentGoldInSafeHasGram: goldInSafe,
            CurrentCashBalanceTL: netCash,
            CashEquivalentHasGram: Math.Round(cashEquivalentGold, 6),
            NetCapitalHasGram: Math.Round(netCapital, 6),
            NetProfitHasGram: Math.Round(netProfit, 6),
            GoldPriceUsed: goldPricePerGram
        );
    }
    
    /// <inheritdoc />
    public async Task<decimal> GetInitialCapitalAsync(CancellationToken cancellationToken = default)
    {
        // İlk "Ana Sermaye" (Capital) kasa hareketini bul
        var allMovements = await _unitOfWork.SafeMovements.GetAllAsync(cancellationToken);
        var capitalMovement = allMovements
            .Where(m => m.MovementType == SafeMovementType.Capital)
            .OrderBy(m => m.TransactionDate)
            .ThenBy(m => m.CreatedAt)
            .FirstOrDefault();
        
        return capitalMovement?.HasGram ?? 0m;
    }
    
    /// <inheritdoc />
    public async Task<CashBalanceResult> GetCashBalanceAsync(CancellationToken cancellationToken = default)
    {
        var transactions = await _unitOfWork.Transactions.GetAllAsync(cancellationToken);
        
        var totalSales = transactions
            .Where(t => t.Direction == TransactionDirection.Sale && t.Price.HasValue)
            .Sum(t => t.Price!.Value);
        
        var totalPurchases = transactions
            .Where(t => t.Direction == TransactionDirection.Purchase && t.Price.HasValue)
            .Sum(t => t.Price!.Value);
        
        var netCash = totalSales - totalPurchases;
        
        return new CashBalanceResult(
            TotalSalesCash: totalSales,
            TotalPurchasesCash: totalPurchases,
            NetCashBalance: netCash
        );
    }

    public async Task<PeriodTransactionSummary> GetPeriodTransactionSummaryAsync(
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken = default)
    {
        var transactions = await _unitOfWork.Transactions.GetByDateRangeAsync(periodStart, periodEnd, cancellationToken);

        var details = new List<TransactionDetail>();
        decimal totalPurchasesHas = 0;
        decimal totalSalesHas = 0;
        decimal totalPurchasesCash = 0;
        decimal totalSalesCash = 0;

        foreach (var tx in transactions)
        {
            var cashImpact = tx.Price ?? 0;
            var direction = tx.Direction == TransactionDirection.Sale ? "Satış" : "Alış";

            if (tx.Direction == TransactionDirection.Sale)
            {
                totalSalesHas += tx.HasGram;
                totalSalesCash += cashImpact;
            }
            else if (tx.Direction == TransactionDirection.Purchase)
            {
                totalPurchasesHas += tx.HasGram;
                totalPurchasesCash += cashImpact;
                cashImpact = -cashImpact; // Alışta nakit azalır
            }

            details.Add(new TransactionDetail(
                Id: tx.Id,
                Date: tx.TransactionDate,
                Direction: direction,
                Quantity: tx.Quantity,
                Milyem: tx.Milyem,
                HasGram: tx.HasGram,
                Price: tx.Price ?? 0,
                CashImpact: cashImpact,
                CustomerName: tx.Customer?.Name,
                Description: tx.Description
            ));
        }

        var netCashChange = totalSalesCash - totalPurchasesCash;
        var netGoldChange = totalPurchasesHas - totalSalesHas;

        return new PeriodTransactionSummary(
            PeriodStart: periodStart,
            PeriodEnd: periodEnd,
            Transactions: details,
            TotalPurchasesHasGram: totalPurchasesHas,
            TotalSalesHasGram: totalSalesHas,
            TotalPurchasesCash: totalPurchasesCash,
            TotalSalesCash: totalSalesCash,
            NetCashChange: netCashChange,
            NetGoldChange: netGoldChange
        );
    }
}
