using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Services;

public class ProfitCalculationService : IProfitCalculationService
{
    private readonly IUnitOfWork _unitOfWork;

    public ProfitCalculationService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ProfitSummary> CalculateProfitAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        // Get all transactions in date range
        var allTransactions = await _unitOfWork.Transactions.GetAllAsync(cancellationToken).ConfigureAwait(false);
        
        var transactionsInRange = allTransactions
            .Where(t => t.TransactionDate.Date >= startDate.Date && t.TransactionDate.Date <= endDate.Date)
            .ToList();

        // Calculate total sales Has
        var totalSalesHas = transactionsInRange
            .Where(t => t.Direction == TransactionDirection.Sale)
            .Sum(t => t.HasGram);

        // Calculate total purchases Has
        var totalPurchasesHas = transactionsInRange
            .Where(t => t.Direction == TransactionDirection.Purchase)
            .Sum(t => t.HasGram);

        // Net profit = Sales - Purchases
        var netProfitHas = totalSalesHas - totalPurchasesHas;

        return new ProfitSummary(
            TotalGoldSalesHas: totalSalesHas,
            TotalGoldPurchasesHas: totalPurchasesHas,
            NetProfitHas: netProfitHas,
            StartDate: startDate,
            EndDate: endDate
        );
    }
}
