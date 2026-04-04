using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Interfaces;

public interface ILedgerRepository : IRepository<LedgerEntry>
{
    Task<decimal> GetGoldBalanceAsync(CancellationToken cancellationToken = default);
    Task<decimal> GetCashBalanceAsync(CancellationToken cancellationToken = default);
    Task<decimal> GetGoldBalanceByPeriodAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<decimal> GetCashBalanceByPeriodAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<decimal> GetGoldBalanceByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<decimal> GetCashBalanceByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<LedgerEntry>> GetByPeriodAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IEnumerable<LedgerEntry>> GetByCustomerAndPeriodAsync(Guid customerId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<bool> AnyEntryForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
}
