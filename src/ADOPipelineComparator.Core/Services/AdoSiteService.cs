using ADOPipelineComparator.Core.Interfaces;
using ADOPipelineComparator.Core.Models;

namespace ADOPipelineComparator.Core.Services;

public sealed class AdoSiteService : IAdoSiteService
{
    private readonly IAdoSiteRepository _repository;

    public AdoSiteService(IAdoSiteRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<AdoSiteViewModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var records = await _repository.GetAllAsync(cancellationToken);
        return records.Select(MapToViewModel).ToList();
    }

    public async Task<List<AdoSiteViewModel>> GetByOrganizationAsync(int organizationId, CancellationToken cancellationToken = default)
    {
        var records = await _repository.GetByOrganizationIdAsync(organizationId, cancellationToken);
        return records.Select(MapToViewModel).ToList();
    }

    public Task SetActiveAsync(int siteId, bool isActive, CancellationToken cancellationToken = default)
    {
        return _repository.SetActiveAsync(siteId, isActive, cancellationToken);
    }

    public Task DeleteAsync(int siteId, CancellationToken cancellationToken = default)
    {
        return _repository.DeleteAsync(siteId, cancellationToken);
    }

    private static AdoSiteViewModel MapToViewModel(AdoSiteRecord record) => new()
    {
        Id = record.Id,
        OrganizationId = record.OrganizationId,
        OrganizationName = record.OrganizationName,
        ProjectName = record.ProjectName,
        IsActive = record.IsActive,
        CreatedAtUtc = record.CreatedAtUtc,
        UpdatedAtUtc = record.UpdatedAtUtc,
    };
}
