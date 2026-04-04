using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <param name="includeInactive">false = yalnızca seçilebilir (aktif) cariler; true = pasif dahil tümü (bakiye toplamları için).</param>
    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken = default, bool includeInactive = true);
    Task<IReadOnlyList<Customer>> GetByTypeAsync(CustomerType type, CancellationToken cancellationToken = default, bool includeInactive = true);
    Task<Customer> AddAsync(Customer entity, CancellationToken cancellationToken = default);
    void Update(Customer entity);
    void Remove(Customer entity);
}
