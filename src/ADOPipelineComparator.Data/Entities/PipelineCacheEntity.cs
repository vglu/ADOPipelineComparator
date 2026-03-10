namespace ADOPipelineComparator.Data.Entities;

public sealed class PipelineCacheEntity
{
    public int Id { get; set; }

    public int AdoSiteId { get; set; }

    public string OrganizationName { get; set; } = string.Empty;

    public string Project { get; set; } = string.Empty;

    public int PipelineId { get; set; }

    public string PipelineName { get; set; } = string.Empty;

    public string PipelineType { get; set; } = string.Empty;

    public string PipelineSubtype { get; set; } = string.Empty;

    public DateTime? LastRunDateUtc { get; set; }

    public string? LastRunBy { get; set; }

    public string? TaskName { get; set; }

    public string PipelineUrl { get; set; } = string.Empty;

    public DateTime CachedAtUtc { get; set; }

    public AdoSiteEntity? AdoSite { get; set; }
}
