using ADOPipelineComparator.Core.Models;

namespace ADOPipelineComparator.Core.Interfaces;

public interface IAdoSiteService
{
    Task<List<AdoSiteViewModel>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<List<AdoSiteViewModel>> GetByOrganizationAsync(int organizationId, CancellationToken cancellationToken = default);

    Task SetActiveAsync(int siteId, bool isActive, CancellationToken cancellationToken = default);

    Task DeleteAsync(int siteId, CancellationToken cancellationToken = default);
}
