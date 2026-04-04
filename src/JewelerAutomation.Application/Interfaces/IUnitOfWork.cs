namespace JewelerAutomation.Application.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    IAuditLogRepository AuditLogs { get; }
    IDailyGoldRateRepository DailyGoldRates { get; }
    ICustomerRepository Customers { get; }
    ITransactionRepository Transactions { get; }
    ISafeMovementRepository SafeMovements { get; }
    IInventoryRepository Inventories { get; }
    ICustomerMovementRepository CustomerMovements { get; }
    ICustomerTransactionRepository CustomerTransactions { get; }
    ICashPeggingLogRepository CashPeggingLogs { get; }
    ILedgerRepository Ledger { get; }
    ICashToGoldConversionRepository CashToGoldConversions { get; }
    IProductTemplateRepository ProductTemplates { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    Task ExecuteRawSqlAsync(string sql, CancellationToken cancellationToken = default);
}
