using ADOPipelineComparator.Core.Models;

namespace ADOPipelineComparator.Core.Interfaces;

public interface IPipelineCacheRepository
{
    Task<List<PipelineCacheRecord>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<List<PipelineCacheRecord>> GetBySiteIdAsync(int adoSiteId, CancellationToken cancellationToken = default);

    Task<PipelineCacheRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task UpsertManyAsync(int adoSiteId, IEnumerable<PipelineCacheRecord> records, CancellationToken cancellationToken = default);

    Task UpsertAsync(PipelineCacheRecord record, CancellationToken cancellationToken = default);
}
