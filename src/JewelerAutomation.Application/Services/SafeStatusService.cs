using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Services;

public class SafeStatusService : ISafeStatusService
{
    private readonly IUnitOfWork _unitOfWork;

    public SafeStatusService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<SafeStatus> GetSafeStatusAsync(CancellationToken cancellationToken = default)
    {
        // 1. Actual Gold: Kasadaki gerçek altın (SafeMovements toplamı)
        var actualGold = await _unitOfWork.SafeMovements
            .GetTotalHasGramBalanceAsync(cancellationToken)
            .ConfigureAwait(false);

        // 2. Expected Gold & Cash Balance: Transaction'lardan hesapla
        var transactions = await _unitOfWork.Transactions.GetAllAsync(cancellationToken).ConfigureAwait(false);
        
        decimal expectedGold = 0;
        decimal cashBalance = 0;

        foreach (var tx in transactions)
        {
            if (tx.Direction == TransactionDirection.Sale)
            {
                // Satış: Altın azalır (-), Nakit artar (+)
                expectedGold -= tx.HasGram;
                if (tx.Price.HasValue)
                {
                    cashBalance += tx.Price.Value;
                }
            }
            else if (tx.Direction == TransactionDirection.Purchase)
            {
                // Alış: Altın artar (+), Nakit azalır (-)
                expectedGold += tx.HasGram;
                if (tx.Price.HasValue)
                {
                    cashBalance -= tx.Price.Value;
                }
            }
        }

        // 3. Gold Shortage: Beklenen - Gerçek
        var goldShortage = expectedGold - actualGold;

        return new SafeStatus(
            GoldBalance: actualGold,
            CashBalance: cashBalance,
            ExpectedGold: expectedGold,
            ActualGold: actualGold,
            GoldShortage: goldShortage
        );
    }
}
