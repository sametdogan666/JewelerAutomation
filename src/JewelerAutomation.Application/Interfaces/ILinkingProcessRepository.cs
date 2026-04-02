using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Interfaces;

public interface ILinkingProcessRepository : IRepository<LinkingProcess>
{
    Task<LinkingProcess?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LinkingProcess>> GetAllOrderedAsync(CancellationToken cancellationToken = default);
}
