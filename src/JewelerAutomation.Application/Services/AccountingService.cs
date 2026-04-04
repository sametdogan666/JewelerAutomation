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
    private readonly ILedgerService _ledger;
    
    private const decimal MilyemFactor = 0.001m;
    private const decimal LabourFactor = 0.01m;
    private const decimal MilyemThreshold = 916m;
    
    public AccountingService(IUnitOfWork unitOfWork, ILedgerService ledger)
    {
        _unitOfWork = unitOfWork;
        _ledger = ledger;
    }

    /// <inheritdoc />
    public decimal CalculateTotalLabour(int pieceCount, decimal unitLabour, bool subtract = false)
    {
        var labour = (decimal)pieceCount * unitLabour * LabourFactor;
        return subtract ? -labour : labour;
    }

    /// <inheritdoc />
    public decimal CalculateHasGramWithLabour(decimal quantity, decimal milyem, decimal totalLabour)
    {
        var hasFromPurity = GramAndMilyemToHasGram(quantity, milyem);
        return Math.Round(hasFromPurity + totalLabour, 6);
    }

    /// <inheritdoc />
    public decimal CalculateHasGram(decimal quantity, decimal milyem)
    {
        return GramAndMilyemToHasGram(quantity, milyem);
    }

    /// <summary>
    /// Brüt gr × ayar → has gr. Milyem 0–1 aralığında ondalık saflık (0,916); üzerinde binlik (916) kabul edilir.
    /// </summary>
    private static decimal GramAndMilyemToHasGram(decimal gram, decimal milyem)
    {
        if (milyem <= 1m)
            return Math.Round(gram * milyem, 6);
        return Math.Round(gram * milyem * MilyemFactor, 6);
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
        var initialCapital = await GetInitialCapitalAsync(cancellationToken);
        
        var balances = await _ledger.GetBalancesAsync(cancellationToken);
        var goldInSafe = balances.TotalGoldBalance;
        var netCash = balances.TotalCashBalance;
        
        var cashEquivalentGold = goldPricePerGram > 0 ? netCash / goldPricePerGram : 0;
        
        var netCapital = cashEquivalentGold + goldInSafe;
        
        return new AccountingProfitResult(
            InitialCapitalHasGram: initialCapital,
            CurrentGoldInSafeHasGram: Math.Round(goldInSafe, 6),
            CurrentCashBalanceTL: Math.Round(netCash, 6),
            CashEquivalentHasGram: Math.Round(cashEquivalentGold, 6),
            NetCapitalHasGram: Math.Round(netCapital, 6),
            NetProfitHasGram: Math.Round(netCapital, 6),
            GoldPriceUsed: goldPricePerGram
        );
    }
    
    /// <inheritdoc />
    public async Task<decimal> GetInitialCapitalAsync(CancellationToken cancellationToken = default)
    {
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
        var balances = await _ledger.GetBalancesAsync(cancellationToken);
        var transactions = await _unitOfWork.Transactions.GetAllAsync(cancellationToken);

        decimal totalSales = 0;
        decimal totalPurchases = 0;

        foreach (var tx in transactions)
        {
            if (tx.Items.Any())
            {
                foreach (var item in tx.Items)
                {
                    if (item.Price.HasValue)
                    {
                        if (item.Direction == TransactionDirection.Sale)
                            totalSales += item.Price.Value;
                        else
                            totalPurchases += item.Price.Value;
                    }
                }
            }
            else
            {
                if (tx.Price.HasValue)
                {
                    if (tx.Direction == TransactionDirection.Sale)
                        totalSales += tx.Price.Value;
                    else
                        totalPurchases += tx.Price.Value;
                }
            }
        }

        return new CashBalanceResult(
            TotalSalesCash: totalSales,
            TotalPurchasesCash: totalPurchases,
            NetCashBalance: balances.TotalCashBalance
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
            if (tx.Items.Any())
            {
                foreach (var item in tx.Items)
                {
                    var cashImpact = item.Price ?? 0;
                    var direction = item.Direction == TransactionDirection.Sale ? "Satış" : "Alış";

                    if (item.Direction == TransactionDirection.Sale)
                    {
                        totalSalesHas += item.HasGram;
                        totalSalesCash += cashImpact;
                    }
                    else
                    {
                        totalPurchasesHas += item.HasGram;
                        totalPurchasesCash += cashImpact;
                        cashImpact = -cashImpact;
                    }

                    details.Add(new TransactionDetail(
                        Id: tx.Id,
                        Date: tx.TransactionDate,
                        Direction: direction,
                        Quantity: item.Quantity,
                        Milyem: item.Milyem,
                        HasGram: item.HasGram,
                        Price: item.Price ?? 0,
                        CashImpact: cashImpact,
                        CustomerName: tx.Customer?.Name,
                        Description: item.Description ?? tx.Description
                    ));
                }
            }
            else
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
                    cashImpact = -cashImpact;
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
