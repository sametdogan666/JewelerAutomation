using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Services;

public class LedgerService : ILedgerService
{
    private readonly IUnitOfWork _unitOfWork;

    public LedgerService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task RecordTransactionAsync(
        DateTime transactionDate,
        TransactionDirection direction,
        decimal goldHasAmount,
        decimal? cashAmount,
        Guid referenceId,
        Guid? customerId,
        string? description,
        Guid? correlationId = null,
        CashCurrency cashCurrency = CashCurrency.Try,
        CancellationToken cancellationToken = default)
    {
        if (direction == TransactionDirection.Sale)
        {
            await _unitOfWork.Ledger.AddAsync(new LedgerEntry
            {
                TransactionDate = transactionDate,
                EntryType = LedgerEntryType.GoldOut,
                GoldHasAmount = goldHasAmount,
                CashAmount = 0,
                CashCurrency = CashCurrency.Try,
                ReferenceType = LedgerReferenceType.Transaction,
                ReferenceId = referenceId,
                CustomerId = customerId,
                Description = description,
                CorrelationId = correlationId
            }, cancellationToken);

            if (cashAmount.HasValue && cashAmount.Value > 0)
            {
                await _unitOfWork.Ledger.AddAsync(new LedgerEntry
                {
                    TransactionDate = transactionDate,
                    EntryType = LedgerEntryType.CashIn,
                    GoldHasAmount = 0,
                    CashAmount = cashAmount.Value,
                    CashCurrency = cashCurrency,
                    ReferenceType = LedgerReferenceType.Transaction,
                    ReferenceId = referenceId,
                    CustomerId = customerId,
                    Description = description,
                    CorrelationId = correlationId
                }, cancellationToken);
            }
        }
        else if (direction == TransactionDirection.Purchase)
        {
            await _unitOfWork.Ledger.AddAsync(new LedgerEntry
            {
                TransactionDate = transactionDate,
                EntryType = LedgerEntryType.GoldIn,
                GoldHasAmount = goldHasAmount,
                CashAmount = 0,
                CashCurrency = CashCurrency.Try,
                ReferenceType = LedgerReferenceType.Transaction,
                ReferenceId = referenceId,
                CustomerId = customerId,
                Description = description,
                CorrelationId = correlationId
            }, cancellationToken);

            if (cashAmount.HasValue && cashAmount.Value > 0)
            {
                await _unitOfWork.Ledger.AddAsync(new LedgerEntry
                {
                    TransactionDate = transactionDate,
                    EntryType = LedgerEntryType.CashOut,
                    GoldHasAmount = 0,
                    CashAmount = cashAmount.Value,
                    CashCurrency = cashCurrency,
                    ReferenceType = LedgerReferenceType.Transaction,
                    ReferenceId = referenceId,
                    CustomerId = customerId,
                    Description = description,
                    CorrelationId = correlationId
                }, cancellationToken);
            }
        }
    }

    public async Task RecordCurrencyExchangeAsync(
        DateTime transactionDate,
        CashCurrency sellCurrency,
        decimal sellAmount,
        CashCurrency buyCurrency,
        decimal buyAmount,
        Guid referenceId,
        string? description,
        CancellationToken cancellationToken = default)
    {
        if (sellCurrency == buyCurrency)
            throw new ArgumentException("Satış ve alış para birimleri farklı olmalıdır.");
        if (sellAmount <= 0 || buyAmount <= 0)
            throw new ArgumentException("Tutarlar sıfırdan büyük olmalıdır.");

        await _unitOfWork.Ledger.AddAsync(new LedgerEntry
        {
            TransactionDate = transactionDate,
            EntryType = LedgerEntryType.CashOut,
            GoldHasAmount = 0,
            CashAmount = sellAmount,
            CashCurrency = sellCurrency,
            ReferenceType = LedgerReferenceType.CurrencyExchange,
            ReferenceId = referenceId,
            CustomerId = null,
            Description = description
        }, cancellationToken);

        await _unitOfWork.Ledger.AddAsync(new LedgerEntry
        {
            TransactionDate = transactionDate,
            EntryType = LedgerEntryType.CashIn,
            GoldHasAmount = 0,
            CashAmount = buyAmount,
            CashCurrency = buyCurrency,
            ReferenceType = LedgerReferenceType.CurrencyExchange,
            ReferenceId = referenceId,
            CustomerId = null,
            Description = description
        }, cancellationToken);
    }

    public async Task RecordForexTradeAgainstTryAsync(
        DateTime transactionDate,
        CashCurrency baseCurrency,
        bool isBuy,
        decimal amountBase,
        decimal counterTryAbs,
        Guid referenceId,
        string? description,
        CancellationToken cancellationToken = default)
    {
        if (baseCurrency == CashCurrency.Try)
            throw new ArgumentException("Döviz işleminde temel para birimi TL olamaz.");
        if (amountBase <= 0 || counterTryAbs <= 0)
            throw new ArgumentException("Tutarlar sıfırdan büyük olmalıdır.");

        if (isBuy)
        {
            await _unitOfWork.Ledger.AddAsync(new LedgerEntry
            {
                TransactionDate = transactionDate,
                EntryType = LedgerEntryType.CashOut,
                GoldHasAmount = 0,
                CashAmount = counterTryAbs,
                CashCurrency = CashCurrency.Try,
                ReferenceType = LedgerReferenceType.Transaction,
                ReferenceId = referenceId,
                CustomerId = null,
                Description = description
            }, cancellationToken);

            await _unitOfWork.Ledger.AddAsync(new LedgerEntry
            {
                TransactionDate = transactionDate,
                EntryType = LedgerEntryType.CashIn,
                GoldHasAmount = 0,
                CashAmount = amountBase,
                CashCurrency = baseCurrency,
                ReferenceType = LedgerReferenceType.Transaction,
                ReferenceId = referenceId,
                CustomerId = null,
                Description = description
            }, cancellationToken);
        }
        else
        {
            await _unitOfWork.Ledger.AddAsync(new LedgerEntry
            {
                TransactionDate = transactionDate,
                EntryType = LedgerEntryType.CashOut,
                GoldHasAmount = 0,
                CashAmount = amountBase,
                CashCurrency = baseCurrency,
                ReferenceType = LedgerReferenceType.Transaction,
                ReferenceId = referenceId,
                CustomerId = null,
                Description = description
            }, cancellationToken);

            await _unitOfWork.Ledger.AddAsync(new LedgerEntry
            {
                TransactionDate = transactionDate,
                EntryType = LedgerEntryType.CashIn,
                GoldHasAmount = 0,
                CashAmount = counterTryAbs,
                CashCurrency = CashCurrency.Try,
                ReferenceType = LedgerReferenceType.Transaction,
                ReferenceId = referenceId,
                CustomerId = null,
                Description = description
            }, cancellationToken);
        }
    }

    public async Task RecordShopCashInAsync(
        DateTime transactionDate,
        decimal cashAmount,
        CashCurrency cashCurrency,
        Guid referenceId,
        string? description,
        Guid? correlationId,
        CancellationToken cancellationToken = default)
    {
        if (cashAmount <= 0)
            return;
        await _unitOfWork.Ledger.AddAsync(new LedgerEntry
        {
            TransactionDate = transactionDate,
            EntryType = LedgerEntryType.CashIn,
            GoldHasAmount = 0,
            CashAmount = cashAmount,
            CashCurrency = cashCurrency,
            ReferenceType = LedgerReferenceType.Transaction,
            ReferenceId = referenceId,
            CustomerId = null,
            Description = description,
            CorrelationId = correlationId
        }, cancellationToken);
    }

    public async Task RecordShopGoldInAsync(
        DateTime transactionDate,
        decimal goldHasAmount,
        Guid referenceId,
        string? description,
        Guid? correlationId,
        CancellationToken cancellationToken = default)
    {
        if (goldHasAmount <= 0)
            return;
        await _unitOfWork.Ledger.AddAsync(new LedgerEntry
        {
            TransactionDate = transactionDate,
            EntryType = LedgerEntryType.GoldIn,
            GoldHasAmount = goldHasAmount,
            CashAmount = 0,
            CashCurrency = CashCurrency.Try,
            ReferenceType = LedgerReferenceType.Transaction,
            ReferenceId = referenceId,
            CustomerId = null,
            Description = description,
            CorrelationId = correlationId
        }, cancellationToken);
    }

    public async Task RecordCustomerTransactionAsync(
        DateTime transactionDate,
        CustomerTransactionType transactionType,
        decimal goldHasAmount,
        decimal cashAmount,
        Guid customerId,
        Guid referenceId,
        string? description,
        CashCurrency cashCurrency = CashCurrency.Try,
        CancellationToken cancellationToken = default)
    {
        switch (transactionType)
        {
            case CustomerTransactionType.GoldPurchase:
                await _unitOfWork.Ledger.AddAsync(new LedgerEntry
                {
                    TransactionDate = transactionDate,
                    EntryType = LedgerEntryType.GoldIn,
                    GoldHasAmount = goldHasAmount,
                    CashAmount = 0,
                    CashCurrency = CashCurrency.Try,
                    ReferenceType = LedgerReferenceType.CustomerTransaction,
                    ReferenceId = referenceId,
                    CustomerId = customerId,
                    Description = description
                }, cancellationToken);
                break;

            case CustomerTransactionType.GoldSale:
                await _unitOfWork.Ledger.AddAsync(new LedgerEntry
                {
                    TransactionDate = transactionDate,
                    EntryType = LedgerEntryType.GoldOut,
                    GoldHasAmount = goldHasAmount,
                    CashAmount = 0,
                    CashCurrency = CashCurrency.Try,
                    ReferenceType = LedgerReferenceType.CustomerTransaction,
                    ReferenceId = referenceId,
                    CustomerId = customerId,
                    Description = description
                }, cancellationToken);
                break;

            case CustomerTransactionType.CashPayment:
                await _unitOfWork.Ledger.AddAsync(new LedgerEntry
                {
                    TransactionDate = transactionDate,
                    EntryType = LedgerEntryType.CashIn,
                    GoldHasAmount = 0,
                    CashAmount = cashAmount,
                    CashCurrency = cashCurrency,
                    ReferenceType = LedgerReferenceType.CustomerTransaction,
                    ReferenceId = referenceId,
                    CustomerId = customerId,
                    Description = description
                }, cancellationToken);
                break;

            case CustomerTransactionType.CashCollection:
                await _unitOfWork.Ledger.AddAsync(new LedgerEntry
                {
                    TransactionDate = transactionDate,
                    EntryType = LedgerEntryType.CashOut,
                    GoldHasAmount = 0,
                    CashAmount = cashAmount,
                    CashCurrency = cashCurrency,
                    ReferenceType = LedgerReferenceType.CustomerTransaction,
                    ReferenceId = referenceId,
                    CustomerId = customerId,
                    Description = description
                }, cancellationToken);
                break;
        }
    }

    public async Task RecordSafeMovementAsync(
        DateTime transactionDate,
        SafeMovementType movementType,
        decimal goldHasAmount,
        Guid referenceId,
        string? description,
        CancellationToken cancellationToken = default)
    {
        if (movementType == SafeMovementType.ProfitRealization)
            return;

        var entryType = movementType switch
        {
            SafeMovementType.Income => LedgerEntryType.GoldIn,
            SafeMovementType.Expense => LedgerEntryType.GoldOut,
            SafeMovementType.Capital => LedgerEntryType.GoldIn,
            SafeMovementType.Transfer => goldHasAmount >= 0 ? LedgerEntryType.GoldIn : LedgerEntryType.GoldOut,
            SafeMovementType.LinkingProfit => LedgerEntryType.GoldIn,
            _ => throw new ArgumentException($"Unknown movement type: {movementType}")
        };

        await _unitOfWork.Ledger.AddAsync(new LedgerEntry
        {
            TransactionDate = transactionDate,
            EntryType = entryType,
            GoldHasAmount = Math.Abs(goldHasAmount),
            CashAmount = 0,
            CashCurrency = CashCurrency.Try,
            ReferenceType = LedgerReferenceType.SafeMovement,
            ReferenceId = referenceId,
            CustomerId = null,
            Description = description
        }, cancellationToken);
    }

    public async Task RecordLinkingFifoPurchaseAsync(
        DateTime transactionDate,
        decimal cashAmount,
        decimal goldHasAmount,
        Guid linkingProcessId,
        string? description,
        CashCurrency cashCurrency = CashCurrency.Try,
        CancellationToken cancellationToken = default)
    {
        await _unitOfWork.Ledger.AddAsync(new LedgerEntry
        {
            TransactionDate = transactionDate,
            EntryType = LedgerEntryType.CashOut,
            GoldHasAmount = 0,
            CashAmount = cashAmount,
            CashCurrency = cashCurrency,
            ReferenceType = LedgerReferenceType.LinkingProcess,
            ReferenceId = linkingProcessId,
            CustomerId = null,
            Description = description
        }, cancellationToken);

        await _unitOfWork.Ledger.AddAsync(new LedgerEntry
        {
            TransactionDate = transactionDate,
            EntryType = LedgerEntryType.GoldIn,
            GoldHasAmount = goldHasAmount,
            CashAmount = 0,
            CashCurrency = CashCurrency.Try,
            ReferenceType = LedgerReferenceType.LinkingProcess,
            ReferenceId = linkingProcessId,
            CustomerId = null,
            Description = description
        }, cancellationToken);
    }

    public async Task RecordCashPeggingAsync(
        DateTime transactionDate,
        decimal cashAmount,
        decimal goldHasAmount,
        Guid referenceId,
        string? description,
        CashCurrency cashCurrency = CashCurrency.Try,
        CancellationToken cancellationToken = default)
    {
        await _unitOfWork.Ledger.AddAsync(new LedgerEntry
        {
            TransactionDate = transactionDate,
            EntryType = LedgerEntryType.CashOut,
            GoldHasAmount = 0,
            CashAmount = cashAmount,
            CashCurrency = cashCurrency,
            ReferenceType = LedgerReferenceType.CashPegging,
            ReferenceId = referenceId,
            CustomerId = null,
            Description = description
        }, cancellationToken);

        await _unitOfWork.Ledger.AddAsync(new LedgerEntry
        {
            TransactionDate = transactionDate,
            EntryType = LedgerEntryType.GoldIn,
            GoldHasAmount = goldHasAmount,
            CashAmount = 0,
            CashCurrency = CashCurrency.Try,
            ReferenceType = LedgerReferenceType.CashPegging,
            ReferenceId = referenceId,
            CustomerId = null,
            Description = description
        }, cancellationToken);
    }

    public async Task RecordCashToGoldConversionAsync(
        DateTime transactionDate,
        decimal cashAmount,
        decimal goldHasAmount,
        Guid referenceId,
        Guid? customerId,
        string? description,
        CashCurrency cashCurrency = CashCurrency.Try,
        CancellationToken cancellationToken = default)
    {
        await _unitOfWork.Ledger.AddAsync(new LedgerEntry
        {
            TransactionDate = transactionDate,
            EntryType = LedgerEntryType.CashOut,
            GoldHasAmount = 0,
            CashAmount = cashAmount,
            CashCurrency = cashCurrency,
            ReferenceType = LedgerReferenceType.CashToGoldConversion,
            ReferenceId = referenceId,
            CustomerId = customerId,
            Description = description
        }, cancellationToken);

        await _unitOfWork.Ledger.AddAsync(new LedgerEntry
        {
            TransactionDate = transactionDate,
            EntryType = LedgerEntryType.GoldIn,
            GoldHasAmount = goldHasAmount,
            CashAmount = 0,
            CashCurrency = CashCurrency.Try,
            ReferenceType = LedgerReferenceType.CashToGoldConversion,
            ReferenceId = referenceId,
            CustomerId = customerId,
            Description = description
        }, cancellationToken);
    }

    public async Task<LedgerBalances> GetBalancesAsync(CancellationToken cancellationToken = default)
    {
        var totalGold = await _unitOfWork.Ledger.GetGoldBalanceAsync(cancellationToken);
        var totalCash = await _unitOfWork.Ledger.GetCashBalanceAsync(cancellationToken);

        return new LedgerBalances(
            TotalGoldBalance: totalGold,
            TotalCashBalance: totalCash,
            SafeGoldBalance: totalGold,
            SafeCashBalance: totalCash
        );
    }

    public async Task<LedgerBalances> GetBalancesByPeriodAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var periodGold = await _unitOfWork.Ledger.GetGoldBalanceByPeriodAsync(startDate, endDate, cancellationToken);
        var periodCash = await _unitOfWork.Ledger.GetCashBalanceByPeriodAsync(startDate, endDate, cancellationToken);

        return new LedgerBalances(
            TotalGoldBalance: periodGold,
            TotalCashBalance: periodCash,
            SafeGoldBalance: periodGold,
            SafeCashBalance: periodCash
        );
    }

    public async Task<CustomerLedgerBalances> GetCustomerBalancesAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var goldBalance = await _unitOfWork.Ledger.GetGoldBalanceByCustomerAsync(customerId, cancellationToken);
        var cashBalance = await _unitOfWork.Ledger.GetCashBalanceByCustomerAsync(customerId, cancellationToken);

        return new CustomerLedgerBalances(
            CustomerId: customerId,
            GoldBalance: goldBalance,
            CashBalance: cashBalance
        );
    }

    public async Task<decimal> GetSafeGoldBalanceAsync(CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Ledger.GetGoldBalanceAsync(cancellationToken);
    }

    public async Task<decimal> GetSafeCashBalanceAsync(CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Ledger.GetCashBalanceAsync(cancellationToken);
    }

    public async Task DeleteEntriesByReferenceAsync(LedgerReferenceType referenceType, Guid referenceId, CancellationToken cancellationToken = default)
    {
        var entries = await _unitOfWork.Ledger.FindAsync(
            e => e.ReferenceType == referenceType && e.ReferenceId == referenceId,
            cancellationToken
        );

        foreach (var entry in entries)
        {
            _unitOfWork.Ledger.Remove(entry);
        }
    }

    public async Task DeleteEntriesByCorrelationAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        var entries = await _unitOfWork.Ledger.FindAsync(
            e => e.CorrelationId == correlationId,
            cancellationToken
        );

        foreach (var entry in entries)
        {
            _unitOfWork.Ledger.Remove(entry);
        }
    }
}
