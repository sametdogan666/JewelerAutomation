using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Interfaces;

public interface ICustomerTransactionRepository
{
    Task<CustomerTransaction> AddAsync(CustomerTransaction entity, CancellationToken cancellationToken = default);
    Task<CustomerTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerTransaction>> GetStatementAsync(Guid customerId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
    Task<(decimal GoldBalance, decimal CashBalance)> GetBalanceAsync(Guid customerId, CancellationToken cancellationToken = default);
    void Update(CustomerTransaction entity);
    void Delete(CustomerTransaction entity);
}
