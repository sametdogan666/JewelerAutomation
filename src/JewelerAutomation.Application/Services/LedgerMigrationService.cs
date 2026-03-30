using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;
using Microsoft.Extensions.Logging;

namespace JewelerAutomation.Application.Services;

public interface ILedgerMigrationService
{
    Task MigrateExistingDataToLedgerAsync(CancellationToken cancellationToken = default);
    Task RebuildLedgerAsync(CancellationToken cancellationToken = default);
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
        _logger.LogInformation("Checking ledger state...");

        var existingEntries = await _unitOfWork.Ledger.GetAllAsync(cancellationToken);
        if (existingEntries.Any())
        {
            _logger.LogInformation("Ledger contains {Count} entries, skipping migration.", existingEntries.Count);
            return;
        }

        _logger.LogInformation("Ledger is empty. Running full migration...");
        await BuildLedgerFromSourceAsync(cancellationToken);
    }

    public async Task RebuildLedgerAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Rebuilding ledger from scratch...");

        // Hard-delete ghost SafeMovements (bypass soft-delete)
        await _unitOfWork.ExecuteRawSqlAsync(
            "DELETE FROM \"SafeMovements\" WHERE \"SourceTransactionId\" IS NULL AND \"Description\" LIKE '%Dönemsel Nakit Bağlama%'",
            cancellationToken);

        // Hard-delete all ledger entries (bypass soft-delete) for a clean rebuild
        await _unitOfWork.ExecuteRawSqlAsync("DELETE FROM \"LedgerEntries\"", cancellationToken);

        _logger.LogInformation("Cleared all ledger entries (hard delete).");

        await BuildLedgerFromSourceAsync(cancellationToken);
    }

    private async Task BuildLedgerFromSourceAsync(CancellationToken cancellationToken)
    {
        var migratedCount = 0;

        var safeMovements = await _unitOfWork.SafeMovements.GetAllAsync(cancellationToken);
        _logger.LogInformation("Migrating {Count} safe movements (non-transaction)...", safeMovements.Count(m => !m.SourceTransactionId.HasValue));

        foreach (var sm in safeMovements)
        {
            if (sm.SourceTransactionId.HasValue)
                continue;

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

        var transactions = await _unitOfWork.Transactions.GetAllAsync(cancellationToken);
        _logger.LogInformation("Migrating {Count} transactions...", transactions.Count);

        foreach (var tx in transactions)
        {
            if (tx.Items.Any())
            {
                foreach (var item in tx.Items)
                {
                    await _ledgerService.RecordTransactionAsync(
                        transactionDate: tx.TransactionDate,
                        direction: item.Direction,
                        goldHasAmount: item.HasGram,
                        cashAmount: item.Price,
                        referenceId: tx.Id,
                        customerId: tx.CustomerId,
                        description: item.Description ?? tx.Description,
                        correlationId: tx.CorrelationId,
                        cancellationToken: cancellationToken
                    );
                    migratedCount++;
                }
            }
            else
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
        }

        var customerTransactions = await _unitOfWork.CustomerTransactions.GetAllAsync(cancellationToken);
        _logger.LogInformation("Migrating {Count} customer transactions...", customerTransactions.Count);

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

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var balances = await _ledgerService.GetBalancesAsync(cancellationToken);
        _logger.LogInformation(
            "Ledger migration complete. Entries={Count}, GoldBalance={Gold}, CashBalance={Cash}",
            migratedCount, balances.TotalGoldBalance, balances.TotalCashBalance);
    }
}
