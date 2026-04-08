namespace JewelerAutomation.Application.Interfaces;

public enum CustomerDeleteResult
{
    NotFound,
    BlockedNonZeroBalance,
    SoftDeleted,
    HardDeleted
}

public interface ICustomerService
{
    Task<CustomerDeleteResult> TryDeleteCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
}
