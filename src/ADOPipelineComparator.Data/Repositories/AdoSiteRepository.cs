using ADOPipelineComparator.Core.Interfaces;
using ADOPipelineComparator.Core.Models;
using ADOPipelineComparator.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADOPipelineComparator.Data.Repositories;

public sealed class AdoSiteRepository : IAdoSiteRepository
{
    private readonly AppDbContext _dbContext;

    public AdoSiteRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<AdoSiteRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.AdoSites
            .AsNoTracking()
            .Include(x => x.Organization)
            .OrderBy(x => x.Organization!.Name)
            .ThenBy(x => x.ProjectName)
            .ToListAsync(cancellationToken);
        return entities.Select(MapToRecord).ToList();
    }

    public async Task<List<AdoSiteRecord>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.AdoSites
            .AsNoTracking()
            .Include(x => x.Organization)
            .Where(x => x.IsActive && x.Organization!.IsActive)
            .OrderBy(x => x.Organization!.Name)
            .ThenBy(x => x.ProjectName)
            .ToListAsync(cancellationToken);
        return entities.Select(MapToRecord).ToList();
    }

    public async Task<List<AdoSiteRecord>> GetByOrganizationIdAsync(int organizationId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.AdoSites
            .AsNoTracking()
            .Include(x => x.Organization)
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.ProjectName)
            .ToListAsync(cancellationToken);
        return entities.Select(MapToRecord).ToList();
    }

    public async Task<AdoSiteRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.AdoSites
            .AsNoTracking()
            .Include(x => x.Organization)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : MapToRecord(entity);
    }

    public async Task<AdoSiteRecord> UpsertByProjectNameAsync(AdoSiteRecord site, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.AdoSites
            .FirstOrDefaultAsync(x => x.OrganizationId == site.OrganizationId && x.ProjectName == site.ProjectName, cancellationToken);

        if (entity is null)
        {
            entity = new AdoSiteEntity
            {
                OrganizationId = site.OrganizationId,
                ProjectName = site.ProjectName,
                IsActive = site.IsActive,
                CreatedAtUtc = site.CreatedAtUtc,
                UpdatedAtUtc = site.UpdatedAtUtc,
            };
            _dbContext.AdoSites.Add(entity);
        }
        else
        {
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var loaded = await _dbContext.AdoSites
            .AsNoTracking()
            .Include(x => x.Organization)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);
        return MapToRecord(loaded);
    }

    public async Task SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.AdoSites.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.IsActive = isActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.AdoSites.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        _dbContext.AdoSites.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AdoSiteRecord MapToRecord(AdoSiteEntity entity) => new()
    {
        Id = entity.Id,
        OrganizationId = entity.OrganizationId,
        OrganizationName = entity.Organization?.Name ?? string.Empty,
        OrganizationUrl = entity.Organization?.OrganizationUrl ?? string.Empty,
        EncryptedPat = entity.Organization?.Pat ?? string.Empty,
        ProjectName = entity.ProjectName,
        IsActive = entity.IsActive,
        CreatedAtUtc = entity.CreatedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc,
    };
}
