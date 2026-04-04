using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Application.Utilities;
using JewelerAutomation.Core.Entities;
using Microsoft.Extensions.Logging;

namespace JewelerAutomation.Application.Services;

/// <summary>
/// Panel özeti: defter (Ledger) ve ham tablolardan doğrudan toplamlar.
/// Manuel kur yalnızca üst etiket için okunur; çekirdek miktarlarla çarpılmaz.
/// </summary>
public class DashboardRawSummaryService : IDashboardSummaryService
{
    private readonly IUnitOfWork _uow;
    private readonly IGoldRatesRepository _goldRatesTable;
    private readonly IGoldLinkingService _goldLinking;
    private readonly ILogger<DashboardRawSummaryService> _logger;

    public DashboardRawSummaryService(
        IUnitOfWork uow,
        IGoldRatesRepository goldRatesTable,
        IGoldLinkingService goldLinking,
        ILogger<DashboardRawSummaryService> logger)
    {
        _uow = uow;
        _goldRatesTable = goldRatesTable;
        _goldLinking = goldLinking;
        _logger = logger;
    }

    private static bool IsHybridPeggingTransaction(Transaction tx) =>
        tx.CorrelationId.HasValue
        && !tx.Items.Any()
        && tx.CashAmount.HasValue
        && tx.EquivalentHasGram.HasValue;

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await BuildAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard raw summary failed.");
            return DashboardSummaryDtoDefaults.Empty;
        }
    }

    private async Task<DashboardSummaryDto> BuildAsync(CancellationToken cancellationToken)
    {
        var ledgerGold = await _uow.Ledger.GetGoldBalanceAsync(cancellationToken).ConfigureAwait(false);
        var ledgerCash = await _uow.Ledger.GetCashBalanceAsync(cancellationToken).ConfigureAwait(false);

        var allMovements = await _uow.SafeMovements.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var transactions = await _uow.Transactions.GetAllAsync(cancellationToken).ConfigureAwait(false);

        decimal transactionNetHasSum = 0;
        foreach (var tx in transactions)
        {
            if (IsHybridPeggingTransaction(tx))
                continue;
            transactionNetHasSum += tx.NetHasGram;
        }

        decimal capitalGold = 0;
        foreach (var sm in allMovements)
        {
            if (sm.SourceTransactionId.HasValue)
                continue;
            capitalGold += sm.MovementType switch
            {
                SafeMovementType.Capital => sm.HasGram,
                SafeMovementType.Income => sm.HasGram,
                SafeMovementType.Expense => -Math.Abs(sm.HasGram),
                SafeMovementType.Transfer => sm.HasGram,
                SafeMovementType.ProfitRealization => 0m,
                SafeMovementType.LinkingProfit => 0m,
                _ => 0m
            };
        }

        decimal tradingPurchases = 0, tradingSales = 0;
        foreach (var tx in transactions)
        {
            if (IsHybridPeggingTransaction(tx))
                continue;

            if (tx.Items.Any())
            {
                foreach (var item in tx.Items)
                {
                    if (item.Direction == TransactionDirection.Purchase)
                        tradingPurchases += item.HasGram;
                    else
                        tradingSales += item.HasGram;
                }
            }
            else
            {
                if (tx.Direction == TransactionDirection.Purchase)
                    tradingPurchases += tx.HasGram;
                else if (tx.Direction == TransactionDirection.Sale)
                    tradingSales += tx.HasGram;
            }
        }

        var openUnpeggedSalesGram = await _goldLinking
            .GetOpenHasPositionAsync(null, null, cancellationToken)
            .ConfigureAwait(false);

        var expectedGold = capitalGold + tradingPurchases - tradingSales;
        var goldGapOrSurplus = Math.Round(-openUnpeggedSalesGram, 6);

        var customers = await _uow.Customers.GetAllAsync(cancellationToken).ConfigureAwait(false);

        decimal customerDebt = 0, customerReceivable = 0;
        decimal personalDebt = 0, personalReceivable = 0;

        foreach (var c in customers)
        {
            var (goldBalance, _) = await _uow.CustomerTransactions
                .GetBalanceAsync(c.Id, cancellationToken)
                .ConfigureAwait(false);

            if (c.Type == CustomerType.Cari)
            {
                if (goldBalance > 0)
                    customerDebt += goldBalance;
                else if (goldBalance < 0)
                    customerReceivable += Math.Abs(goldBalance);
            }
            else if (c.Type == CustomerType.Sahis)
            {
                if (goldBalance > 0)
                    personalDebt += goldBalance;
                else if (goldBalance < 0)
                    personalReceivable += Math.Abs(goldBalance);
            }
        }

        var netGold = ledgerGold + customerReceivable + personalReceivable - customerDebt - personalDebt;
        var netCash = ledgerCash;

        var initialCapitalMovement = allMovements
            .Where(m => m.MovementType == SafeMovementType.Capital)
            .OrderBy(m => m.TransactionDate)
            .ThenBy(m => m.CreatedAt)
            .FirstOrDefault();
        var initialCapital = initialCapitalMovement?.HasGram ?? 0m;
        var profitHasGram = netGold - initialCapital;

        var profitRealizations = allMovements
            .Where(m => m.MovementType == SafeMovementType.ProfitRealization)
            .ToList();
        var cumulativePeggingProfit = profitRealizations.Sum(m => m.HasGram);
        var peggingCount = profitRealizations.Count;

        var netGoldCapital = ledgerGold + customerReceivable + personalReceivable - customerDebt - personalDebt;

        var todayTr = TurkeyClock.TodayDateOnly();
        var manualRow = await _goldRatesTable
            .GetByEffectiveDateAsync(todayTr, isManual: true, cancellationToken)
            .ConfigureAwait(false);

        decimal? labelMid = null;
        DateTime? labelFetchedAt = null;
        var fromManual = false;
        if (manualRow != null && manualRow.HasTryPerGramMid > 0)
        {
            labelMid = manualRow.HasTryPerGramMid;
            labelFetchedAt = manualRow.RecordedAtUtc;
            fromManual = true;
        }

        Console.WriteLine($"[Dashboard] Ledger Gold (safe, Has): {ledgerGold:F6}");
        Console.WriteLine($"[Dashboard] Ledger Cash (TL): {ledgerCash:F2}");
        Console.WriteLine($"[Dashboard] Transactions Σ NetHasGram (excl. hybrid): {transactionNetHasSum:F6}");
        Console.WriteLine($"[Dashboard] Cari borç / alacak (Has): {customerDebt:F6} / {customerReceivable:F6}");
        Console.WriteLine($"[Dashboard] Şahıs borç / alacak (Has): {personalDebt:F6} / {personalReceivable:F6}");
        Console.WriteLine($"[Dashboard] Net Altın (Has): {netGold:F6}  Net Nakit (TL): {netCash:F2}");
        Console.WriteLine($"[Dashboard] ExpectedGold (kasa+işlem): {expectedGold:F6}  GoldGap/Surplus: {goldGapOrSurplus:F6}");
        Console.WriteLine($"[Dashboard] Manuel kur (etiket): {(labelMid?.ToString("F2") ?? "yok")}");

        return new DashboardSummaryDto(
            NetGoldCapitalHasGram: netGoldCapital,
            TotalGoldInSafe: ledgerGold,
            TotalCashInSafe: ledgerCash,
            TotalCustomerGoldDebt: customerDebt,
            TotalCustomerGoldReceivable: customerReceivable,
            TotalPersonalGoldDebt: personalDebt,
            TotalPersonalGoldReceivable: personalReceivable,
            PhysicalGoldBalance: ledgerGold,
            PhysicalCashBalance: ledgerCash,
            NetGoldPositionHasGram: netGold,
            NetCashPositionTl: netCash,
            ExpectedGold: expectedGold,
            GoldGapOrSurplus: goldGapOrSurplus,
            ProfitHasGram: profitHasGram,
            CumulativePeggingProfitHasGram: cumulativePeggingProfit,
            PeggingCount: peggingCount,
            LiveHasTryPerGramMid: labelMid,
            LiveUsdTryMid: null,
            RatesFetchedAtUtc: labelFetchedAt,
            RatesAvailable: labelMid.HasValue,
            RatesFromHistoricalFallback: false,
            RatesFromDefaultFallback: false,
            RatesFromManualOverride: fromManual,
            NetSermayeHasGramAtLivePrice: null,
            NetGoldPositionTlApprox: null);
    }
}
