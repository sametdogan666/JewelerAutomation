using JewelerAutomation.Application.Interfaces;

namespace JewelerAutomation.Application.Services;

/// <summary>
/// Calculates jewelry store net capital (Net Sermaye):
/// - Gold in Safe (Kasa)
/// - Customer gold debts/receivables (Cari - Commercial)
/// - Personal gold debts/receivables (Şahıs - Personal)
/// Net Gold Capital = Gold in Safe + All Receivables - All Debts
/// </summary>
public class CapitalCalculationService : ICapitalCalculationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledger;

    public CapitalCalculationService(IUnitOfWork unitOfWork, ILedgerService ledger)
    {
        _unitOfWork = unitOfWork;
        _ledger = ledger;
    }

    public async Task<CapitalSummary> GetCapitalSummaryAsync(CancellationToken cancellationToken = default)
    {
        // 1. Kasadaki altın — tüm SafeMovements (manuel + sepet) imzalı toplamı; panel ile aynı.
        var totalGoldInSafe = await _unitOfWork.SafeMovements
            .GetPhysicalVaultNetHasGramAsync(cancellationToken)
            .ConfigureAwait(false);

        // 2. Kasadaki nakit — defter CashIn/CashOut toplamı (SafeMovement kayıtları üzerinden).
        var totalCashInSafe = await _ledger.GetSafeCashBalanceAsync(cancellationToken).ConfigureAwait(false);

        // 3. Tüm müşterileri al ve tipine göre grupla
        var customers = await _unitOfWork.Customers.GetAllAsync(cancellationToken);

        // Cari hesaplar (Commercial - Ticari müşteri/tedarikçi)
        var commercialCustomers = customers.Where(c => c.Type == Core.Entities.CustomerType.Cari).ToList();
        decimal totalCommercialDebt = 0;
        decimal totalCommercialReceivable = 0;

        foreach (var customer in commercialCustomers)
        {
            var goldBalance = (await _unitOfWork.CustomerTransactions.GetBalanceAsync(customer.Id, cancellationToken)).GoldHasGram;
            if (goldBalance > 0)
                totalCommercialDebt += goldBalance; // Müşteriye altın borcumuz
            else if (goldBalance < 0)
                totalCommercialReceivable += Math.Abs(goldBalance); // Müşteriden altın alacağımız
        }

        // Şahıs hesaplar (Personal - Arkadaş, aile, özel borç/alacak)
        var personalCustomers = customers.Where(c => c.Type == Core.Entities.CustomerType.Sahis).ToList();
        decimal totalPersonalDebt = 0;
        decimal totalPersonalReceivable = 0;

        foreach (var customer in personalCustomers)
        {
            var goldBalance = (await _unitOfWork.CustomerTransactions.GetBalanceAsync(customer.Id, cancellationToken)).GoldHasGram;
            if (goldBalance > 0)
                totalPersonalDebt += goldBalance; // Şahsa altın borcumuz
            else if (goldBalance < 0)
                totalPersonalReceivable += Math.Abs(goldBalance); // Şahıstan altın alacağımız
        }

        // 4. Net Gold Capital = GoldInSafe + AllReceivables - AllDebts
        var netGoldCapital = totalGoldInSafe 
            + totalCommercialReceivable 
            + totalPersonalReceivable 
            - totalCommercialDebt 
            - totalPersonalDebt;

        return new CapitalSummary(
            TotalGoldInSafe: totalGoldInSafe,
            TotalCashInSafe: totalCashInSafe,
            TotalCustomerGoldDebt: totalCommercialDebt,
            TotalCustomerGoldReceivable: totalCommercialReceivable,
            TotalPersonalGoldDebt: totalPersonalDebt,
            TotalPersonalGoldReceivable: totalPersonalReceivable,
            NetGoldCapital: netGoldCapital
        );
    }
}
