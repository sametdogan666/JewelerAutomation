using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Infrastructure.Data;

namespace JewelerAutomation.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(
        AppDbContext context,
        IUserRepository users,
        ICustomerRepository customers,
        ITransactionRepository transactions,
        ISafeMovementRepository safeMovements,
        IInventoryRepository inventories,
        ICustomerMovementRepository customerMovements,
        ICustomerTransactionRepository customerTransactions)
    {
        _context = context;
        Users = users;
        Customers = customers;
        Transactions = transactions;
        SafeMovements = safeMovements;
        Inventories = inventories;
        CustomerMovements = customerMovements;
        CustomerTransactions = customerTransactions;
    }

    public IUserRepository Users { get; }
    public ICustomerRepository Customers { get; }
    public ITransactionRepository Transactions { get; }
    public ISafeMovementRepository SafeMovements { get; }
    public IInventoryRepository Inventories { get; }
    public ICustomerMovementRepository CustomerMovements { get; }
    public ICustomerTransactionRepository CustomerTransactions { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public void Dispose() => _context.Dispose();
}
