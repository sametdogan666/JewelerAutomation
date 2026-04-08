namespace JewelerAutomation.Application.Interfaces;

public interface ISafeStatusService
{
    Task<SafeStatus> GetSafeStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Physical Balance (Brüt Kasa): actual gold/cash from direct movements.
/// Net Position (Finansal Durum): physical + customer credits - customer debts.
/// Gold Gap/Surplus: net difference of all gold transactions.
/// Profit is reporting-only, never added to physical balances.
/// </summary>
public record SafeStatus(
    // Physical Balance (Brüt Kasa)
    decimal PhysicalGoldBalance,
    decimal PhysicalCashBalance,
    decimal PhysicalCashBalanceUsd,
    decimal PhysicalCashBalanceEur,
    decimal PhysicalCashBalanceGbp,

    // Gold Gap / Surplus
    decimal ExpectedGold,
    decimal GoldGapOrSurplus,

    // Net Position (Finansal Durum)
    decimal CustomerGoldDebt,
    decimal CustomerGoldReceivable,
    decimal PersonalGoldDebt,
    decimal PersonalGoldReceivable,
    decimal SahisGoldLiabilitiesHasGram,
    decimal NetPhysicalEquityHasGram,
    decimal NetGoldPosition,
    decimal NetCashPosition,
    decimal NetCashPositionUsd,
    decimal NetCashPositionEur,
    decimal NetCashPositionGbp,

    // Profit (reporting-only metric)
    decimal ProfitHasGram,

    // Cumulative Performance (from all ProfitRealization entries)
    decimal CumulativePeggingProfitHasGram,
    int PeggingCount
);
