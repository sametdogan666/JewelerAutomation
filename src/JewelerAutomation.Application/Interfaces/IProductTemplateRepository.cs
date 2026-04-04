using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Interfaces;

public interface IProductTemplateRepository
{
    Task<IReadOnlyList<ProductTemplate>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ProductTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductTemplate> AddAsync(ProductTemplate entity, CancellationToken cancellationToken = default);
    void Update(ProductTemplate entity);
    void Delete(ProductTemplate entity);
}
