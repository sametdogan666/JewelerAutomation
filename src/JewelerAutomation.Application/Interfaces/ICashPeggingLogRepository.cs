using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Interfaces;

public interface ICashPeggingLogRepository
{
    Task<CashPeggingLog> AddAsync(CashPeggingLog entity, CancellationToken cancellationToken = default);
    Task<CashPeggingLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashPeggingLog>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashPeggingLog>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<CashPeggingLog?> GetLatestAsync(CancellationToken cancellationToken = default);
    void Update(CashPeggingLog entity);
    void Delete(CashPeggingLog entity);
}
