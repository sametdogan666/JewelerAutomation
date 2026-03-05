namespace JewelerAutomation.Application.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    ICustomerRepository Customers { get; }
    ITransactionRepository Transactions { get; }
    ISafeMovementRepository SafeMovements { get; }
    IInventoryRepository Inventories { get; }
    ICustomerMovementRepository CustomerMovements { get; }
    ICustomerTransactionRepository CustomerTransactions { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
