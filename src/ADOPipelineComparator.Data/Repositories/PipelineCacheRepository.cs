using ADOPipelineComparator.Core.Interfaces;
using ADOPipelineComparator.Core.Models;
using ADOPipelineComparator.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADOPipelineComparator.Data.Repositories;

public sealed class PipelineCacheRepository : IPipelineCacheRepository
{
    private readonly AppDbContext _dbContext;

    public PipelineCacheRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<PipelineCacheRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.PipelineCache
            .AsNoTracking()
            .OrderBy(x => x.OrganizationName)
            .ThenBy(x => x.Project)
            .ThenBy(x => x.PipelineName)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToRecord).ToList();
    }

    public async Task<List<PipelineCacheRecord>> GetBySiteIdAsync(int adoSiteId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.PipelineCache
            .AsNoTracking()
            .Where(x => x.AdoSiteId == adoSiteId)
            .OrderBy(x => x.Project)
            .ThenBy(x => x.PipelineName)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToRecord).ToList();
    }

    public async Task<PipelineCacheRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.PipelineCache
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : MapToRecord(entity);
    }

    public async Task UpsertManyAsync(int adoSiteId, IEnumerable<PipelineCacheRecord> records, CancellationToken cancellationToken = default)
    {
        var incoming = records.ToList();
        var incomingByKey = incoming.ToDictionary(CreateKey, x => x);

        var existing = await _dbContext.PipelineCache
            .Where(x => x.AdoSiteId == adoSiteId)
            .ToListAsync(cancellationToken);

        foreach (var entity in existing)
        {
            var key = CreateKey(entity);
            if (incomingByKey.TryGetValue(key, out var record))
            {
                ApplyToEntity(entity, record);
                incomingByKey.Remove(key);
            }
            else
            {
                _dbContext.PipelineCache.Remove(entity);
            }
        }

        foreach (var leftover in incomingByKey.Values)
        {
            _dbContext.PipelineCache.Add(MapToEntity(leftover));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertAsync(PipelineCacheRecord record, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.PipelineCache.FirstOrDefaultAsync(
            x => x.AdoSiteId == record.AdoSiteId
                && x.Project == record.Project
                && x.PipelineId == record.PipelineId
                && x.PipelineType == record.PipelineType.ToString(),
            cancellationToken);

        if (entity is null)
        {
            _dbContext.PipelineCache.Add(MapToEntity(record));
        }
        else
        {
            ApplyToEntity(entity, record);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string CreateKey(PipelineCacheRecord record)
    {
        return $"{record.AdoSiteId}|{record.Project}|{record.PipelineId}|{record.PipelineType}";
    }

    private static string CreateKey(PipelineCacheEntity entity)
    {
        return $"{entity.AdoSiteId}|{entity.Project}|{entity.PipelineId}|{entity.PipelineType}";
    }

    private static void ApplyToEntity(PipelineCacheEntity entity, PipelineCacheRecord record)
    {
        entity.OrganizationName = record.OrganizationName;
        entity.Project = record.Project;
        entity.PipelineId = record.PipelineId;
        entity.PipelineName = record.PipelineName;
        entity.PipelineType = record.PipelineType.ToString();
        entity.PipelineSubtype = record.PipelineSubtype;
        entity.LastRunDateUtc = record.LastRunDateUtc;
        entity.LastRunBy = record.LastRunBy;
        entity.TaskName = record.TaskName;
        entity.PipelineUrl = record.PipelineUrl;
        entity.CachedAtUtc = record.CachedAtUtc;
    }

    private static PipelineCacheEntity MapToEntity(PipelineCacheRecord record)
    {
        return new PipelineCacheEntity
        {
            AdoSiteId = record.AdoSiteId,
            OrganizationName = record.OrganizationName,
            Project = record.Project,
            PipelineId = record.PipelineId,
            PipelineName = record.PipelineName,
            PipelineType = record.PipelineType.ToString(),
            PipelineSubtype = record.PipelineSubtype,
            LastRunDateUtc = record.LastRunDateUtc,
            LastRunBy = record.LastRunBy,
            TaskName = record.TaskName,
            PipelineUrl = record.PipelineUrl,
            CachedAtUtc = record.CachedAtUtc,
        };
    }

    private static PipelineCacheRecord MapToRecord(PipelineCacheEntity entity)
    {
        if (!Enum.TryParse<PipelineType>(entity.PipelineType, true, out var pipelineType))
        {
            pipelineType = PipelineType.Build;
        }

        return new PipelineCacheRecord
        {
            Id = entity.Id,
            AdoSiteId = entity.AdoSiteId,
            OrganizationName = entity.OrganizationName,
            Project = entity.Project,
            PipelineId = entity.PipelineId,
            PipelineName = entity.PipelineName,
            PipelineType = pipelineType,
            PipelineSubtype = entity.PipelineSubtype,
            LastRunDateUtc = entity.LastRunDateUtc,
            LastRunBy = entity.LastRunBy,
            TaskName = entity.TaskName,
            PipelineUrl = entity.PipelineUrl,
            CachedAtUtc = entity.CachedAtUtc,
        };
    }
}
