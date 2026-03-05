namespace JewelerAutomation.Application.Interfaces;

/// <summary>
/// Calculates total capital (Net Sermaye) for the jewelry store.
/// </summary>
public interface ICapitalCalculationService
{
    /// <summary>
    /// Get net capital summary including gold in safe, customer debts/receivables, and net gold capital.
    /// </summary>
    Task<CapitalSummary> GetCapitalSummaryAsync(CancellationToken cancellationToken = default);
}

public record CapitalSummary(
    decimal TotalGoldInSafe,
    decimal TotalCashInSafe,
    decimal TotalCustomerGoldDebt,
    decimal TotalCustomerGoldReceivable,
    decimal NetGoldCapital
);
