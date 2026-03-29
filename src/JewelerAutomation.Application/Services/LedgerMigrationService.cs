using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;
using Microsoft.Extensions.Logging;

namespace JewelerAutomation.Application.Services;

public interface ILedgerMigrationService
{
    Task MigrateExistingDataToLedgerAsync(CancellationToken cancellationToken = default);
}

public class LedgerMigrationService : ILedgerMigrationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly ILogger<LedgerMigrationService> _logger;

    public LedgerMigrationService(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService,
        ILogger<LedgerMigrationService> logger)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
        _logger = logger;
    }

    public async Task MigrateExistingDataToLedgerAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting ledger migration from existing data...");

        var existingEntries = await _unitOfWork.Ledger.GetAllAsync(cancellationToken);
        if (existingEntries.Any())
        {
            _logger.LogWarning("Ledger already contains {Count} entries. Skipping migration to avoid duplicates.", existingEntries.Count);
            return;
        }

        var migratedCount = 0;

        var transactions = await _unitOfWork.Transactions.GetAllAsync(cancellationToken);
        _logger.LogInformation("Migrating {Count} transactions to ledger...", transactions.Count);
        
        foreach (var tx in transactions)
        {
            await _ledgerService.RecordTransactionAsync(
                transactionDate: tx.TransactionDate,
                direction: tx.Direction,
                goldHasAmount: tx.HasGram,
                cashAmount: tx.Price,
                referenceId: tx.Id,
                customerId: tx.CustomerId,
                description: tx.Description,
                cancellationToken: cancellationToken
            );
            migratedCount++;
        }

        var customerTransactions = await _unitOfWork.CustomerTransactions.GetAllAsync(cancellationToken);
        _logger.LogInformation("Migrating {Count} customer transactions to ledger...", customerTransactions.Count);
        
        foreach (var ctx in customerTransactions)
        {
            await _ledgerService.RecordCustomerTransactionAsync(
                transactionDate: ctx.TransactionDate,
                transactionType: ctx.TransactionType,
                goldHasAmount: ctx.GoldHas,
                cashAmount: ctx.CashAmount,
                customerId: ctx.CustomerId,
                referenceId: ctx.Id,
                description: ctx.Description,
                cancellationToken: cancellationToken
            );
            migratedCount++;
        }

        var safeMovements = await _unitOfWork.SafeMovements.GetAllAsync(cancellationToken);
        _logger.LogInformation("Migrating {Count} safe movements to ledger...", safeMovements.Count);
        
        foreach (var sm in safeMovements)
        {
            if (sm.SourceTransactionId.HasValue)
            {
                continue;
            }

            await _ledgerService.RecordSafeMovementAsync(
                transactionDate: sm.TransactionDate,
                movementType: sm.MovementType,
                goldHasAmount: sm.HasGram,
                referenceId: sm.Id,
                description: sm.Description,
                cancellationToken: cancellationToken
            );
            migratedCount++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Ledger migration completed. Total entries created: {Count}", migratedCount);
    }
}
