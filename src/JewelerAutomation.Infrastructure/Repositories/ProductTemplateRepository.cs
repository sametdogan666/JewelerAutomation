using Microsoft.EntityFrameworkCore;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;
using JewelerAutomation.Infrastructure.Data;

namespace JewelerAutomation.Infrastructure.Repositories;

public class ProductTemplateRepository : IProductTemplateRepository
{
    private readonly AppDbContext _context;

    public ProductTemplateRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<ProductTemplate>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.ProductTemplates
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task<ProductTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.ProductTemplates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<ProductTemplate> AddAsync(ProductTemplate entity, CancellationToken cancellationToken = default)
    {
        await _context.ProductTemplates.AddAsync(entity, cancellationToken);
        return entity;
    }

    public void Update(ProductTemplate entity) => _context.ProductTemplates.Update(entity);

    public void Delete(ProductTemplate entity) => _context.ProductTemplates.Remove(entity);
}
