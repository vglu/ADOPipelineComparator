using ADOPipelineComparator.Core.Models;

namespace ADOPipelineComparator.Core.Interfaces;

public interface IOrganizationService
{
    Task<List<OrganizationViewModel>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<OrganizationRecord?> GetRecordAsync(int id, CancellationToken cancellationToken = default);

    Task<OrganizationViewModel> CreateAsync(OrganizationUpsertModel request, CancellationToken cancellationToken = default);

    Task<OrganizationViewModel> UpdateAsync(OrganizationUpsertModel request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
