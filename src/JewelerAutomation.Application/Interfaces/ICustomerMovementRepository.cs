using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Interfaces;

public interface ICustomerMovementRepository
{
    Task<CustomerMovement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerMovement>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<decimal> GetBalanceByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<CustomerMovement> AddAsync(CustomerMovement entity, CancellationToken cancellationToken = default);
}
