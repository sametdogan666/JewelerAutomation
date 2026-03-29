using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Services;

public record LedgerBalances(
    decimal TotalGoldBalance,
    decimal TotalCashBalance,
    decimal SafeGoldBalance,
    decimal SafeCashBalance
);

public record CustomerLedgerBalances(
    Guid CustomerId,
    decimal GoldBalance,
    decimal CashBalance
);

public interface ILedgerService
{
    Task RecordTransactionAsync(
        DateTime transactionDate,
        TransactionDirection direction,
        decimal goldHasAmount,
        decimal? cashAmount,
        Guid referenceId,
        Guid? customerId,
        string? description,
        CancellationToken cancellationToken = default);

    Task RecordCustomerTransactionAsync(
        DateTime transactionDate,
        CustomerTransactionType transactionType,
        decimal goldHasAmount,
        decimal cashAmount,
        Guid customerId,
        Guid referenceId,
        string? description,
        CancellationToken cancellationToken = default);

    Task RecordSafeMovementAsync(
        DateTime transactionDate,
        SafeMovementType movementType,
        decimal goldHasAmount,
        Guid referenceId,
        string? description,
        CancellationToken cancellationToken = default);

    Task RecordCashPeggingAsync(
        DateTime transactionDate,
        decimal cashAmount,
        decimal goldHasAmount,
        Guid referenceId,
        string? description,
        CancellationToken cancellationToken = default);

    Task RecordCashToGoldConversionAsync(
        DateTime transactionDate,
        decimal cashAmount,
        decimal goldHasAmount,
        Guid referenceId,
        Guid? customerId,
        string? description,
        CancellationToken cancellationToken = default);

    Task<LedgerBalances> GetBalancesAsync(CancellationToken cancellationToken = default);
    Task<CustomerLedgerBalances> GetCustomerBalancesAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<decimal> GetSafeGoldBalanceAsync(CancellationToken cancellationToken = default);
    Task<decimal> GetSafeCashBalanceAsync(CancellationToken cancellationToken = default);
    Task DeleteEntriesByReferenceAsync(LedgerReferenceType referenceType, Guid referenceId, CancellationToken cancellationToken = default);
}
