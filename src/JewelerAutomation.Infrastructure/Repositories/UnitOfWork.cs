using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JewelerAutomation.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(
        AppDbContext context,
        IUserRepository users,
        IAuditLogRepository auditLogs,
        IDailyGoldRateRepository dailyGoldRates,
        ICustomerRepository customers,
        ITransactionRepository transactions,
        ISafeMovementRepository safeMovements,
        IInventoryRepository inventories,
        ICustomerMovementRepository customerMovements,
        ICustomerTransactionRepository customerTransactions,
        ICashPeggingLogRepository cashPeggingLogs,
        ILedgerRepository ledger,
        ICashToGoldConversionRepository cashToGoldConversions,
        IProductTemplateRepository productTemplates)
    {
        _context = context;
        Users = users;
        AuditLogs = auditLogs;
        DailyGoldRates = dailyGoldRates;
        Customers = customers;
        Transactions = transactions;
        SafeMovements = safeMovements;
        Inventories = inventories;
        CustomerMovements = customerMovements;
        CustomerTransactions = customerTransactions;
        CashPeggingLogs = cashPeggingLogs;
        Ledger = ledger;
        CashToGoldConversions = cashToGoldConversions;
        ProductTemplates = productTemplates;
    }

    public IUserRepository Users { get; }
    public IAuditLogRepository AuditLogs { get; }
    public IDailyGoldRateRepository DailyGoldRates { get; }
    public ICustomerRepository Customers { get; }
    public ITransactionRepository Transactions { get; }
    public ISafeMovementRepository SafeMovements { get; }
    public IInventoryRepository Inventories { get; }
    public ICustomerMovementRepository CustomerMovements { get; }
    public ICustomerTransactionRepository CustomerTransactions { get; }
    public ICashPeggingLogRepository CashPeggingLogs { get; }
    public ILedgerRepository Ledger { get; }
    public ICashToGoldConversionRepository CashToGoldConversions { get; }
    public IProductTemplateRepository ProductTemplates { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        => await _context.Database.BeginTransactionAsync(cancellationToken);

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction != null)
            await _context.Database.CommitTransactionAsync(cancellationToken);
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction != null)
            await _context.Database.RollbackTransactionAsync(cancellationToken);
    }

    public async Task ExecuteRawSqlAsync(string sql, CancellationToken cancellationToken = default)
        => await _context.Database.ExecuteSqlRawAsync(sql, cancellationToken);

    public void Dispose() => _context.Dispose();
}
