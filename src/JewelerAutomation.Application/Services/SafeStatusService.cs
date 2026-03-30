using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Services;

public class SafeStatusService : ISafeStatusService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledger;

    public SafeStatusService(IUnitOfWork unitOfWork, ILedgerService ledger)
    {
        _unitOfWork = unitOfWork;
        _ledger = ledger;
    }

    public async Task<SafeStatus> GetSafeStatusAsync(CancellationToken cancellationToken = default)
    {
        // ── Physical Balance (Brüt Kasa) ──
        var balances = await _ledger.GetBalancesAsync(cancellationToken).ConfigureAwait(false);
        var physicalGold = balances.SafeGoldBalance;
        var physicalCash = balances.SafeCashBalance;

        // ── Expected Gold & Trading Position ──
        // ExpectedGold = all recorded gold sources (should match physicalGold if ledger is consistent)
        // GoldGapOrSurplus = Purchases - Sales (net trading position)

        var allMovements = await _unitOfWork.SafeMovements.GetAllAsync(cancellationToken).ConfigureAwait(false);
        decimal capitalGold = 0;
        foreach (var sm in allMovements)
        {
            if (sm.SourceTransactionId.HasValue) continue;
            capitalGold += sm.MovementType switch
            {
                SafeMovementType.Capital => sm.HasGram,
                SafeMovementType.Income => sm.HasGram,
                SafeMovementType.Expense => -Math.Abs(sm.HasGram),
                SafeMovementType.Transfer => sm.HasGram,
                SafeMovementType.ProfitRealization => 0m,
                _ => 0m
            };
        }

        var transactions = await _unitOfWork.Transactions.GetAllAsync(cancellationToken).ConfigureAwait(false);
        decimal tradingPurchases = 0, tradingSales = 0;
        foreach (var tx in transactions)
        {
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

        var expectedGold = capitalGold + tradingPurchases - tradingSales;
        var goldGapOrSurplus = tradingPurchases - tradingSales;

        // ── Customer / Personal debts & receivables ──
        var customers = await _unitOfWork.Customers.GetAllAsync(cancellationToken).ConfigureAwait(false);

        decimal customerDebt = 0, customerReceivable = 0;
        decimal personalDebt = 0, personalReceivable = 0;

        foreach (var c in customers)
        {
            var (goldBalance, _) = await _unitOfWork.CustomerTransactions
                .GetBalanceAsync(c.Id, cancellationToken).ConfigureAwait(false);

            if (c.Type == CustomerType.Cari)
            {
                if (goldBalance > 0) customerDebt += goldBalance;
                else if (goldBalance < 0) customerReceivable += Math.Abs(goldBalance);
            }
            else if (c.Type == CustomerType.Sahis)
            {
                if (goldBalance > 0) personalDebt += goldBalance;
                else if (goldBalance < 0) personalReceivable += Math.Abs(goldBalance);
            }
        }

        // ── Net Position ──
        var netGold = physicalGold
            + customerReceivable + personalReceivable
            - customerDebt - personalDebt;
        var netCash = physicalCash;

        // ── Profit (reporting-only) ──
        var initialCapitalMovement = allMovements
            .Where(m => m.MovementType == SafeMovementType.Capital)
            .OrderBy(m => m.TransactionDate)
            .ThenBy(m => m.CreatedAt)
            .FirstOrDefault();
        var initialCapital = initialCapitalMovement?.HasGram ?? 0m;
        var profitHasGram = netGold - initialCapital;

        // ── Cumulative Performance (from all ProfitRealization entries) ──
        var profitRealizations = allMovements
            .Where(m => m.MovementType == SafeMovementType.ProfitRealization)
            .ToList();
        var cumulativePeggingProfit = profitRealizations.Sum(m => m.HasGram);
        var peggingCount = profitRealizations.Count;

        return new SafeStatus(
            PhysicalGoldBalance: physicalGold,
            PhysicalCashBalance: physicalCash,
            ExpectedGold: expectedGold,
            GoldGapOrSurplus: goldGapOrSurplus,
            CustomerGoldDebt: customerDebt,
            CustomerGoldReceivable: customerReceivable,
            PersonalGoldDebt: personalDebt,
            PersonalGoldReceivable: personalReceivable,
            NetGoldPosition: netGold,
            NetCashPosition: netCash,
            ProfitHasGram: profitHasGram,
            CumulativePeggingProfitHasGram: cumulativePeggingProfit,
            PeggingCount: peggingCount
        );
    }
}
