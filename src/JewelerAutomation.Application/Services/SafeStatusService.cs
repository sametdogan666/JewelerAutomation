using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Services;

public class SafeStatusService : ISafeStatusService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledger;

    public SafeStatusService(IUnitOfWork unitOfWork, ILedgerService ledger)
    {
        _unitOfWork = unitOfWork;
        _ledger = ledger;
    }

    public async Task<SafeStatus> GetSafeStatusAsync(CancellationToken cancellationToken = default)
    {
        var balances = await _ledger.GetBalancesAsync(cancellationToken).ConfigureAwait(false);
        
        var actualGold = balances.SafeGoldBalance;
        var cashBalance = balances.SafeCashBalance;

        var transactions = await _unitOfWork.Transactions.GetAllAsync(cancellationToken).ConfigureAwait(false);
        decimal expectedGold = 0;

        foreach (var tx in transactions)
        {
            if (tx.Direction == TransactionDirection.Sale)
            {
                expectedGold -= tx.HasGram;
            }
            else if (tx.Direction == TransactionDirection.Purchase)
            {
                expectedGold += tx.HasGram;
            }
        }
        
        var goldShortage = actualGold - expectedGold;

        return new SafeStatus(
            GoldBalance: actualGold,
            CashBalance: cashBalance,
            ExpectedGold: expectedGold,
            ActualGold: actualGold,
            GoldShortage: goldShortage
        );
    }
}
