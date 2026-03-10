using ADOPipelineComparator.Core.Models;

namespace ADOPipelineComparator.Core.Interfaces;

public interface IOrganizationRepository
{
    Task<List<OrganizationRecord>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<List<OrganizationRecord>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<OrganizationRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<OrganizationRecord> CreateAsync(OrganizationRecord org, CancellationToken cancellationToken = default);

    Task UpdateAsync(OrganizationRecord org, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
