using ADOPipelineComparator.Core.Models;

namespace ADOPipelineComparator.Core.Interfaces;

public interface IAdoSiteRepository
{
    Task<List<AdoSiteRecord>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<List<AdoSiteRecord>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<List<AdoSiteRecord>> GetByOrganizationIdAsync(int organizationId, CancellationToken cancellationToken = default);

    Task<AdoSiteRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<AdoSiteRecord> UpsertByProjectNameAsync(AdoSiteRecord site, CancellationToken cancellationToken = default);

    Task SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
