using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Interfaces;

public interface ICashToGoldConversionRepository : IRepository<CashToGoldConversion>
{
    Task<IReadOnlyList<CashToGoldConversion>> GetByPeriodAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashToGoldConversion>> GetByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalConvertedGoldAsync(CancellationToken cancellationToken = default);
}
