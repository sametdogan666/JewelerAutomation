using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Services;

public class PeggingService : IPeggingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledger;
    private readonly ICashPeggingService _cashPegging;
    private readonly IGoldLinkingService _goldLinking;

    public PeggingService(
        IUnitOfWork unitOfWork,
        ILedgerService ledger,
        ICashPeggingService cashPegging,
        IGoldLinkingService goldLinking)
    {
        _unitOfWork = unitOfWork;
        _ledger = ledger;
        _cashPegging = cashPegging;
        _goldLinking = goldLinking;
    }

    public async Task<SafeStatus> ComputeDashboardSafeStatusAsync(CancellationToken cancellationToken = default)
    {
        var balances = await _ledger.GetBalancesAsync(cancellationToken).ConfigureAwait(false);
        var physicalGold = await _unitOfWork.SafeMovements
            .GetPhysicalVaultNetHasGramAsync(cancellationToken)
            .ConfigureAwait(false);
        var physicalCashTry = balances.SafeCashBalance;
        var physicalCashUsd = await _unitOfWork.Ledger
            .GetCashBalanceForCurrencyAsync(CashCurrency.Usd, cancellationToken).ConfigureAwait(false);
        var physicalCashEur = await _unitOfWork.Ledger
            .GetCashBalanceForCurrencyAsync(CashCurrency.Eur, cancellationToken).ConfigureAwait(false);
        var physicalCashGbp = await _unitOfWork.Ledger
            .GetCashBalanceForCurrencyAsync(CashCurrency.Gbp, cancellationToken).ConfigureAwait(false);

        var allMovements = await _unitOfWork.SafeMovements.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var openUnpeggedSalesGram = await _goldLinking
            .GetOpenHasPositionAsync(null, null, cancellationToken)
            .ConfigureAwait(false);

        var expectedGold = physicalGold;
        var goldGapOrSurplus = Math.Round(-openUnpeggedSalesGram, 6);

        var customers = await _unitOfWork.Customers.GetAllAsync(cancellationToken).ConfigureAwait(false);

        decimal customerDebt = 0, customerReceivable = 0;
        decimal personalDebt = 0, personalReceivable = 0;
        decimal sahısGoldLiabilitiesHasGram = 0;

        foreach (var c in customers)
        {
            var book = await _unitOfWork.CustomerTransactions
                .GetBalanceAsync(c.Id, cancellationToken).ConfigureAwait(false);
            var goldBalance = book.GoldHasGram;

            if (c.Type == CustomerType.Cari)
            {
                if (goldBalance > 0) customerDebt += goldBalance;
                else if (goldBalance < 0) customerReceivable += Math.Abs(goldBalance);
            }
            else if (c.Type == CustomerType.Sahis)
            {
                if (goldBalance > 0)
                {
                    personalDebt += goldBalance;
                    sahısGoldLiabilitiesHasGram += goldBalance;
                }
                else if (goldBalance < 0) personalReceivable += Math.Abs(goldBalance);
            }
        }

        var netPhysicalEquityHasGram = physicalGold - sahısGoldLiabilitiesHasGram;

        var netGold = physicalGold
            + customerReceivable + personalReceivable
            - customerDebt - personalDebt;
        var netCashTry = physicalCashTry;
        var netCashUsd = physicalCashUsd;
        var netCashEur = physicalCashEur;
        var netCashGbp = physicalCashGbp;

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

        return new SafeStatus(
            PhysicalGoldBalance: physicalGold,
            PhysicalCashBalance: physicalCashTry,
            PhysicalCashBalanceUsd: physicalCashUsd,
            PhysicalCashBalanceEur: physicalCashEur,
            PhysicalCashBalanceGbp: physicalCashGbp,
            ExpectedGold: expectedGold,
            GoldGapOrSurplus: goldGapOrSurplus,
            CustomerGoldDebt: customerDebt,
            CustomerGoldReceivable: customerReceivable,
            PersonalGoldDebt: personalDebt,
            PersonalGoldReceivable: personalReceivable,
            SahisGoldLiabilitiesHasGram: sahısGoldLiabilitiesHasGram,
            NetPhysicalEquityHasGram: netPhysicalEquityHasGram,
            NetGoldPosition: netGold,
            NetCashPosition: netCashTry,
            NetCashPositionUsd: netCashUsd,
            NetCashPositionEur: netCashEur,
            NetCashPositionGbp: netCashGbp,
            ProfitHasGram: profitHasGram,
            CumulativePeggingProfitHasGram: cumulativePeggingProfit,
            PeggingCount: peggingCount
        );
    }

    public async Task<UnifiedPeggingSimulationDto> SimulateUnifiedAsync(
        UnifiedPeggingSimulateRequest request,
        CancellationToken cancellationToken = default)
    {
        var hybrid = await _cashPegging.SimulatePeggingAsync(
            request.PeriodStart,
            request.PeriodEnd,
            request.GoldPricePerGram,
            request.PegCashFromSafe,
            request.PegHasGram,
            cancellationToken).ConfigureAwait(false);

        var openInPeriod = await _goldLinking.GetOpenHasPositionAsync(
            request.PeriodStart,
            request.PeriodEnd,
            cancellationToken).ConfigureAwait(false);

        var eq = hybrid.CashEquivalentHasGram;
        var estimatedOpenAfterHybrid = Math.Max(0, openInPeriod - Math.Min(openInPeriod, eq));

        FifoLinkingSimulationResult? fifo = null;
        if (request.FifoTargetAmountGram is > 0)
        {
            fifo = await _goldLinking.SimulateFifoLinkingAsync(
                request.FifoTargetAmountGram.Value,
                request.GoldPricePerGram,
                request.PeriodStart,
                request.PeriodEnd,
                cancellationToken).ConfigureAwait(false);
        }

        return new UnifiedPeggingSimulationDto(hybrid, fifo, openInPeriod, estimatedOpenAfterHybrid);
    }

    public Task<CashPeggingLog> CreateHybridPeggingAsync(
        DateTime periodStart,
        DateTime periodEnd,
        decimal goldPricePerGram,
        string? notes,
        Guid? userId,
        decimal? pegCashFromSafe,
        decimal? pegHasGram,
        CancellationToken cancellationToken = default)
        => _cashPegging.CreatePeggingAsync(
            periodStart,
            periodEnd,
            goldPricePerGram,
            notes,
            userId,
            pegCashFromSafe,
            pegHasGram,
            cancellationToken);
}
