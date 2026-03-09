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
        // 1. Gerçek Kasadaki Altın (SafeMovements toplamı - fiziksel sayım)
        var actualGold = await _unitOfWork.SafeMovements
            .GetTotalHasGramBalanceAsync(cancellationToken)
            .ConfigureAwait(false);

        // 2. Beklenen Altın: SADECE Transaction'lardan hesaplanan teorik değer
        // (Manuel sermaye hareketlerini dahil ETME)
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
                    cashBalance += tx.Price.Value;
            }
            else if (tx.Direction == TransactionDirection.Purchase)
            {
                // Alış: Altın artar (+), Nakit azalır (-)
                expectedGold += tx.HasGram;
                if (tx.Price.HasValue)
                    cashBalance -= tx.Price.Value;
            }
        }

        // NOT: Manuel SafeMovements (Ana Sermaye) beklenen altına EKLENMEZ
        // Çünkü "Beklenen Altın" sadece işlemlerden kaynaklanan net değişimi gösterir
        
        // 3. Altın Açığı/Fazlası: Kasadaki - İşlemlerden Beklenen
        // Bu, manuel sermaye + transaction sonucu oluşan fiziksel durum ile
        // sadece transaction'lardan beklenen değişim arasındaki farkı gösterir
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
