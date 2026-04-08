using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Interfaces;

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
        Guid? correlationId = null,
        CashCurrency cashCurrency = CashCurrency.Try,
        CancellationToken cancellationToken = default);

    /// <summary>Kasada bir para biriminden çıkış, diğerine giriş (aynı <paramref name="referenceId"/> ile eşlenir).</summary>
    Task RecordCurrencyExchangeAsync(
        DateTime transactionDate,
        CashCurrency sellCurrency,
        decimal sellAmount,
        CashCurrency buyCurrency,
        decimal buyAmount,
        Guid referenceId,
        string? description,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Döviz ↔ TRY: <paramref name="isBuy"/> true iken kasadan TRY çıkar, döviz girer; false iken tersi.
    /// <paramref name="baseCurrency"/> USD/EUR/GBP olmalıdır. Defter <see cref="LedgerReferenceType.Transaction"/> ile işlem Id’sine bağlanır.
    /// </summary>
    Task RecordForexTradeAgainstTryAsync(
        DateTime transactionDate,
        CashCurrency baseCurrency,
        bool isBuy,
        decimal amountBase,
        decimal counterTryAbs,
        Guid referenceId,
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
        CashCurrency cashCurrency = CashCurrency.Try,
        CancellationToken cancellationToken = default);

    /// <summary>Kasaya yalnız nakit girişi (şahıs emanet satışı vb.).</summary>
    Task RecordShopCashInAsync(
        DateTime transactionDate,
        decimal cashAmount,
        CashCurrency cashCurrency,
        Guid referenceId,
        string? description,
        Guid? correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>Kasaya yalnız has altın girişi (şahıs emanet alışı).</summary>
    Task RecordShopGoldInAsync(
        DateTime transactionDate,
        decimal goldHasAmount,
        Guid referenceId,
        string? description,
        Guid? correlationId,
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
        CashCurrency cashCurrency = CashCurrency.Try,
        CancellationToken cancellationToken = default);

    Task RecordCashToGoldConversionAsync(
        DateTime transactionDate,
        decimal cashAmount,
        decimal goldHasAmount,
        Guid referenceId,
        Guid? customerId,
        string? description,
        CashCurrency cashCurrency = CashCurrency.Try,
        CancellationToken cancellationToken = default);

    /// <summary>FIFO nakit bağlama: kasadan nakit çıkış + satın alınan has altın girişi.</summary>
    Task RecordLinkingFifoPurchaseAsync(
        DateTime transactionDate,
        decimal cashAmount,
        decimal goldHasAmount,
        Guid linkingProcessId,
        string? description,
        CashCurrency cashCurrency = CashCurrency.Try,
        CancellationToken cancellationToken = default);

    Task<LedgerBalances> GetBalancesAsync(CancellationToken cancellationToken = default);
    Task<LedgerBalances> GetBalancesByPeriodAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<CustomerLedgerBalances> GetCustomerBalancesAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<decimal> GetSafeGoldBalanceAsync(CancellationToken cancellationToken = default);
    Task<decimal> GetSafeCashBalanceAsync(CancellationToken cancellationToken = default);
    Task DeleteEntriesByReferenceAsync(LedgerReferenceType referenceType, Guid referenceId, CancellationToken cancellationToken = default);
    Task DeleteEntriesByCorrelationAsync(Guid correlationId, CancellationToken cancellationToken = default);
}
