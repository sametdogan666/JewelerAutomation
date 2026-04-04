using JewelerAutomation.Application.Interfaces;

namespace JewelerAutomation.Application.Services;

public class CustomerService : ICustomerService
{
    public const string NonZeroBalanceMessage =
        "Bakiyesi sıfır olmayan cari silinemez. Lütfen önce hesabı kapatınız.";

    private const decimal GoldTolerance = 0.000001m;
    private const decimal CashTolerance = 0.01m;

    private readonly IUnitOfWork _unitOfWork;

    public CustomerService(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<CustomerDeleteResult> TryDeleteCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId, cancellationToken).ConfigureAwait(false);
        if (customer == null)
            return CustomerDeleteResult.NotFound;

        var (gold, cash) = await _unitOfWork.CustomerTransactions
            .GetBalanceAsync(customerId, cancellationToken).ConfigureAwait(false);
        var movementHas = await _unitOfWork.CustomerMovements
            .GetBalanceByCustomerIdAsync(customerId, cancellationToken).ConfigureAwait(false);

        if (Math.Abs(gold) > GoldTolerance || Math.Abs(cash) > CashTolerance || Math.Abs(movementHas) > GoldTolerance)
            return CustomerDeleteResult.BlockedNonZeroBalance;

        if (!customer.IsActive)
            return CustomerDeleteResult.SoftDeleted;

        if (await HasFinancialHistoryAsync(customerId, cancellationToken).ConfigureAwait(false))
        {
            customer.IsActive = false;
            customer.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Customers.Update(customer);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return CustomerDeleteResult.SoftDeleted;
        }

        // AppDbContext, ISoftDelete için Remove → satırı fiziksel silmez; IsDeleted=true yapar.
        // Finansal kaydı olmayan cari için bu yeterli; FK zinciri zaten boştur.
        _unitOfWork.Customers.Remove(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return CustomerDeleteResult.HardDeleted;
    }

    private async Task<bool> HasFinancialHistoryAsync(Guid customerId, CancellationToken cancellationToken)
    {
        if (await _unitOfWork.Transactions.AnyByCustomerIdAsync(customerId, cancellationToken).ConfigureAwait(false))
            return true;
        if (await _unitOfWork.SafeMovements.AnyVaultMovementForCustomerAsync(customerId, cancellationToken).ConfigureAwait(false))
            return true;
        if (await _unitOfWork.Ledger.AnyEntryForCustomerAsync(customerId, cancellationToken).ConfigureAwait(false))
            return true;
        if (await _unitOfWork.CustomerMovements.AnyForCustomerAsync(customerId, cancellationToken).ConfigureAwait(false))
            return true;
        if (await _unitOfWork.CustomerTransactions.AnyForCustomerAsync(customerId, cancellationToken).ConfigureAwait(false))
            return true;
        if (await _unitOfWork.CashToGoldConversions.AnyForCustomerAsync(customerId, cancellationToken).ConfigureAwait(false))
            return true;
        return false;
    }
}
