namespace JewelerAutomation.Application.Interfaces;

public interface IProfitCalculationService
{
    Task<ProfitSummary> CalculateProfitAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}

public record ProfitSummary(
    decimal TotalGoldSalesHas,
    decimal TotalGoldPurchasesHas,
    decimal NetProfitHas,
    DateTime StartDate,
    DateTime EndDate
);
