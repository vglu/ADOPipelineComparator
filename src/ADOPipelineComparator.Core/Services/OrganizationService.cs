using ADOPipelineComparator.Core.Interfaces;
using ADOPipelineComparator.Core.Models;

namespace ADOPipelineComparator.Core.Services;

public sealed class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _repository;
    private readonly IEncryptionService _encryptionService;

    public OrganizationService(IOrganizationRepository repository, IEncryptionService encryptionService)
    {
        _repository = repository;
        _encryptionService = encryptionService;
    }

    public async Task<List<OrganizationViewModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var records = await _repository.GetAllAsync(cancellationToken);
        return records
            .Select(MapToViewModel)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task<OrganizationRecord?> GetRecordAsync(int id, CancellationToken cancellationToken = default)
    {
        return _repository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<OrganizationViewModel> CreateAsync(OrganizationUpsertModel request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Pat))
        {
            throw new InvalidOperationException("PAT is required when creating an organization.");
        }

        var now = DateTime.UtcNow;
        var record = new OrganizationRecord
        {
            Name = request.Name.Trim(),
            OrganizationUrl = NormalizeOrganizationUrl(request.OrganizationUrl),
            EncryptedPat = _encryptionService.Encrypt(request.Pat.Trim()),
            IsActive = request.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var created = await _repository.CreateAsync(record, cancellationToken);
        return MapToViewModel(created);
    }

    public async Task<OrganizationViewModel> UpdateAsync(OrganizationUpsertModel request, CancellationToken cancellationToken = default)
    {
        if (!request.Id.HasValue)
        {
            throw new InvalidOperationException("Organization id is required for updates.");
        }

        var existing = await _repository.GetByIdAsync(request.Id.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Organization {request.Id.Value} was not found.");

        existing.Name = request.Name.Trim();
        existing.OrganizationUrl = NormalizeOrganizationUrl(request.OrganizationUrl);
        existing.IsActive = request.IsActive;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Pat))
        {
            existing.EncryptedPat = _encryptionService.Encrypt(request.Pat.Trim());
        }

        await _repository.UpdateAsync(existing, cancellationToken);
        return MapToViewModel(existing);
    }

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        return _repository.DeleteAsync(id, cancellationToken);
    }

    private static string NormalizeOrganizationUrl(string organizationUrl)
    {
        var normalized = organizationUrl.Trim().TrimEnd('/');

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("Organization URL must be absolute and start with http:// or https://.");
        }

        return normalized;
    }

    private static OrganizationViewModel MapToViewModel(OrganizationRecord record) => new()
    {
        Id = record.Id,
        Name = record.Name,
        OrganizationUrl = record.OrganizationUrl,
        PatMasked = "********",
        IsActive = record.IsActive,
        CreatedAtUtc = record.CreatedAtUtc,
        UpdatedAtUtc = record.UpdatedAtUtc,
    };
}
