using ADOPipelineComparator.Core.Models;

namespace ADOPipelineComparator.Core.Interfaces;

public interface IAdoService
{
    Task<TestConnectionResult> TestConnectionAsync(OrganizationRecord org, CancellationToken cancellationToken = default);

    Task<(List<AdoSiteViewModel> Sites, List<string> Errors)> SyncSitesFromAdoAsync(int organizationId, CancellationToken cancellationToken = default);

    Task<List<PipelineCacheRecord>> GetCachedPipelinesAsync(CancellationToken cancellationToken = default);

    Task<RefreshSummary> RefreshAllPipelinesAsync(CancellationToken cancellationToken = default);

    Task<RefreshSummary> RefreshPipelinesForSiteAsync(int adoSiteId, CancellationToken cancellationToken = default);

    Task<RefreshSummary> RefreshSinglePipelineAsync(int cacheRecordId, CancellationToken cancellationToken = default);

    Task<List<PipelineDefinitionRecord>> GetPipelineDefinitionsAsync(
        IReadOnlyList<PipelineCacheRecord> pipelines,
        CancellationToken cancellationToken = default);
}
