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
                    ReferenceType = LedgerReferenceType.Transaction,
                    ReferenceId = referenceId,
                    CustomerId = customerId,
                    Description = description,
                    CorrelationId = correlationId
                }, cancellationToken);
            }
        }
    }

    public async Task RecordCustomerTransactionAsync(
        DateTime transactionDate,
        CustomerTransactionType transactionType,
        decimal goldHasAmount,
        decimal cashAmount,
        Guid customerId,
        Guid referenceId,
        string? description,
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
        CancellationToken cancellationToken = default)
    {
        await _unitOfWork.Ledger.AddAsync(new LedgerEntry
        {
            TransactionDate = transactionDate,
            EntryType = LedgerEntryType.CashOut,
            GoldHasAmount = 0,
            CashAmount = cashAmount,
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
        CancellationToken cancellationToken = default)
    {
        await _unitOfWork.Ledger.AddAsync(new LedgerEntry
        {
            TransactionDate = transactionDate,
            EntryType = LedgerEntryType.CashOut,
            GoldHasAmount = 0,
            CashAmount = cashAmount,
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
        CancellationToken cancellationToken = default)
    {
        await _unitOfWork.Ledger.AddAsync(new LedgerEntry
        {
            TransactionDate = transactionDate,
            EntryType = LedgerEntryType.CashOut,
            GoldHasAmount = 0,
            CashAmount = cashAmount,
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
