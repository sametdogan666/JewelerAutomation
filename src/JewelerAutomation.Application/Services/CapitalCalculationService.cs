using JewelerAutomation.Application.Interfaces;

namespace JewelerAutomation.Application.Services;

/// <summary>
/// Calculates jewelry store net capital (Net Sermaye):
/// - Gold in Safe (Kasa)
/// - Customer gold debts (we owe them)
/// - Customer gold receivables (they owe us)
/// Net Gold Capital = Gold in Safe + Receivables - Debts
/// </summary>
public class CapitalCalculationService : ICapitalCalculationService
{
    private readonly IUnitOfWork _unitOfWork;

    public CapitalCalculationService(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<CapitalSummary> GetCapitalSummaryAsync(CancellationToken cancellationToken = default)
    {
        // 1. Kasadaki altın (Safe movements toplamı)
        var safeMovements = await _unitOfWork.SafeMovements.GetAllAsync(cancellationToken);
        var totalGoldInSafe = safeMovements.Sum(m => m.HasGram);

        // 2. Kasadaki nakit (şu an yok; ileride SafeMovement'ta CashAmount eklenirse kullanılır)
        // Şimdilik 0 döndürüyoruz
        var totalCashInSafe = 0m;

        // 3. Tüm müşterilerin altın bakiyelerini topla
        var customers = await _unitOfWork.Customers.GetAllAsync(cancellationToken);
        decimal totalDebt = 0; // Pozitif bakiyeler toplamı (biz müşteriye borçluyuz)
        decimal totalReceivable = 0; // Negatif bakiyeler toplamı (müşteri bize borçlu)

        foreach (var customer in customers)
        {
            var (goldBalance, _) = await _unitOfWork.CustomerTransactions.GetBalanceAsync(customer.Id, cancellationToken);
            if (goldBalance > 0)
                totalDebt += goldBalance; // Müşteriye altın borcumuz
            else if (goldBalance < 0)
                totalReceivable += Math.Abs(goldBalance); // Müşteriden altın alacağımız
        }

        // 4. Net Gold Capital = Kasadaki altın + Alacaklar - Borçlar
        var netGoldCapital = totalGoldInSafe + totalReceivable - totalDebt;

        return new CapitalSummary(
            TotalGoldInSafe: totalGoldInSafe,
            TotalCashInSafe: totalCashInSafe,
            TotalCustomerGoldDebt: totalDebt,
            TotalCustomerGoldReceivable: totalReceivable,
            NetGoldCapital: netGoldCapital
        );
    }
}
