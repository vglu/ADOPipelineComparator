using ADOPipelineComparator.Core.Interfaces;
using ADOPipelineComparator.Core.Models;
using ADOPipelineComparator.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADOPipelineComparator.Data.Repositories;

public sealed class OrganizationRepository : IOrganizationRepository
{
    private readonly AppDbContext _dbContext;

    public OrganizationRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<OrganizationRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Organizations
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return entities.Select(MapToRecord).ToList();
    }

    public async Task<List<OrganizationRecord>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Organizations
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return entities.Select(MapToRecord).ToList();
    }

    public async Task<OrganizationRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : MapToRecord(entity);
    }

    public async Task<OrganizationRecord> CreateAsync(OrganizationRecord org, CancellationToken cancellationToken = default)
    {
        var entity = new OrganizationEntity
        {
            Name = org.Name,
            OrganizationUrl = org.OrganizationUrl,
            Pat = org.EncryptedPat,
            IsActive = org.IsActive,
            CreatedAtUtc = org.CreatedAtUtc,
            UpdatedAtUtc = org.UpdatedAtUtc,
        };
        _dbContext.Organizations.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToRecord(entity);
    }

    public async Task UpdateAsync(OrganizationRecord org, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Organizations
            .FirstOrDefaultAsync(x => x.Id == org.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Organization {org.Id} was not found.");

        entity.Name = org.Name;
        entity.OrganizationUrl = org.OrganizationUrl;
        entity.Pat = org.EncryptedPat;
        entity.IsActive = org.IsActive;
        entity.UpdatedAtUtc = org.UpdatedAtUtc;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Organizations
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        _dbContext.Organizations.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static OrganizationRecord MapToRecord(OrganizationEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        OrganizationUrl = entity.OrganizationUrl,
        EncryptedPat = entity.Pat,
        IsActive = entity.IsActive,
        CreatedAtUtc = entity.CreatedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc,
    };
}
