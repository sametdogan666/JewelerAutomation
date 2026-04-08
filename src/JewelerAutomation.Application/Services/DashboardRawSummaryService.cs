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

    private static IReadOnlyList<PhysicalVaultHistoryPointDto> BuildPhysicalVaultHistory(IEnumerable<SafeMovement> movements)
    {
        var ordered = movements
            .OrderBy(m => m.TransactionDate)
            .ThenBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToList();
        decimal cum = 0;
        var list = new List<PhysicalVaultHistoryPointDto>(ordered.Count);
        foreach (var m in ordered)
        {
            cum += SafeMovementPhysicalVault.GetSignedHasGramContribution(m);
            list.Add(new PhysicalVaultHistoryPointDto(m.TransactionDate, Math.Round(cum, 6)));
        }

        return list;
    }

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
        var physicalVaultGold = await _uow.SafeMovements
            .GetPhysicalVaultNetHasGramAsync(cancellationToken)
            .ConfigureAwait(false);
        var ledgerCashTry = await _uow.Ledger.GetCashBalanceForCurrencyAsync(CashCurrency.Try, cancellationToken).ConfigureAwait(false);
        var ledgerCashUsd = await _uow.Ledger.GetCashBalanceForCurrencyAsync(CashCurrency.Usd, cancellationToken).ConfigureAwait(false);
        var ledgerCashEur = await _uow.Ledger.GetCashBalanceForCurrencyAsync(CashCurrency.Eur, cancellationToken).ConfigureAwait(false);
        var ledgerCashGbp = await _uow.Ledger.GetCashBalanceForCurrencyAsync(CashCurrency.Gbp, cancellationToken).ConfigureAwait(false);

        var allMovements = await _uow.SafeMovements.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var transactions = await _uow.Transactions.GetAllAsync(cancellationToken).ConfigureAwait(false);

        decimal transactionNetHasSum = 0;
        foreach (var tx in transactions)
        {
            if (IsHybridPeggingTransaction(tx))
                continue;
            transactionNetHasSum += tx.NetHasGram;
        }

        var openUnpeggedSalesGram = await _goldLinking
            .GetOpenHasPositionAsync(null, null, cancellationToken)
            .ConfigureAwait(false);

        var expectedGold = physicalVaultGold;
        var goldGapOrSurplus = Math.Round(-openUnpeggedSalesGram, 6);
        var vaultHistory = BuildPhysicalVaultHistory(allMovements);

        var customers = await _uow.Customers.GetAllAsync(cancellationToken).ConfigureAwait(false);

        decimal customerDebt = 0, customerReceivable = 0;
        decimal personalDebt = 0, personalReceivable = 0;
        decimal sahısGoldLiabilitiesHasGram = 0;

        foreach (var c in customers)
        {
            var book = await _uow.CustomerTransactions
                .GetBalanceAsync(c.Id, cancellationToken)
                .ConfigureAwait(false);
            var goldBalance = book.GoldHasGram;

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
                {
                    personalDebt += goldBalance;
                    sahısGoldLiabilitiesHasGram += goldBalance;
                }
                else if (goldBalance < 0)
                    personalReceivable += Math.Abs(goldBalance);
            }
        }

        var netPhysicalEquityHasGram = physicalVaultGold - sahısGoldLiabilitiesHasGram;

        var netGold = physicalVaultGold + customerReceivable + personalReceivable - customerDebt - personalDebt;
        var netCashTry = ledgerCashTry;
        var netCashUsd = ledgerCashUsd;
        var netCashEur = ledgerCashEur;
        var netCashGbp = ledgerCashGbp;

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

        var netGoldCapital = physicalVaultGold + customerReceivable + personalReceivable - customerDebt - personalDebt;

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

        Console.WriteLine($"[Dashboard] Ledger Gold (defter, Has): {ledgerGold:F6}");
        Console.WriteLine($"[Dashboard] Fiziki kasa (SafeMovements, Has): {physicalVaultGold:F6}");
        Console.WriteLine($"[Dashboard] Ledger Cash TL/USD/EUR/GBP: {ledgerCashTry:F2} / {ledgerCashUsd:F2} / {ledgerCashEur:F2} / {ledgerCashGbp:F2}");
        Console.WriteLine($"[Dashboard] Transactions Σ NetHasGram (excl. hybrid): {transactionNetHasSum:F6}");
        Console.WriteLine($"[Dashboard] Cari borç / alacak (Has): {customerDebt:F6} / {customerReceivable:F6}");
        Console.WriteLine($"[Dashboard] Şahıs borç / alacak (Has): {personalDebt:F6} / {personalReceivable:F6}");
        Console.WriteLine($"[Dashboard] Net Altın (Has): {netGold:F6}  Net Nakit TL/USD/EUR/GBP: {netCashTry:F2} / {netCashUsd:F2} / {netCashEur:F2} / {netCashGbp:F2}");
        Console.WriteLine($"[Dashboard] Fiziki brüt (özet): {expectedGold:F6}  GoldGap/Surplus: {goldGapOrSurplus:F6}");
        Console.WriteLine($"[Dashboard] Manuel kur (etiket): {(labelMid?.ToString("F2") ?? "yok")}");

        return new DashboardSummaryDto(
            NetGoldCapitalHasGram: netGoldCapital,
            TotalGoldInSafe: physicalVaultGold,
            TotalCashInSafe: ledgerCashTry,
            TotalCashInSafeUsd: ledgerCashUsd,
            TotalCashInSafeEur: ledgerCashEur,
            TotalCashInSafeGbp: ledgerCashGbp,
            TotalCustomerGoldDebt: customerDebt,
            TotalCustomerGoldReceivable: customerReceivable,
            TotalPersonalGoldDebt: personalDebt,
            TotalPersonalGoldReceivable: personalReceivable,
            SahisGoldLiabilitiesHasGram: sahısGoldLiabilitiesHasGram,
            NetPhysicalEquityHasGram: netPhysicalEquityHasGram,
            PhysicalGoldBalance: physicalVaultGold,
            PhysicalCashBalance: ledgerCashTry,
            PhysicalCashBalanceUsd: ledgerCashUsd,
            PhysicalCashBalanceEur: ledgerCashEur,
            PhysicalCashBalanceGbp: ledgerCashGbp,
            NetGoldPositionHasGram: netGold,
            NetCashPositionTl: netCashTry,
            NetCashPositionUsd: netCashUsd,
            NetCashPositionEur: netCashEur,
            NetCashPositionGbp: netCashGbp,
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
            NetGoldPositionTlApprox: null,
            PhysicalVaultHistory: vaultHistory);
    }
}
