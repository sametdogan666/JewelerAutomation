using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;
using JewelerAutomation.Infrastructure.Data;

namespace JewelerAutomation.Infrastructure.Repositories;

public class LinkingDetailRepository : Repository<LinkingDetail>, IRepository<LinkingDetail>
{
    public LinkingDetailRepository(AppDbContext context) : base(context)
    {
    }
}
