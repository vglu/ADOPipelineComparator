namespace ADOPipelineComparator.Data.Entities;

public sealed class AdoSiteEntity
{
    public int Id { get; set; }

    public int OrganizationId { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = false;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public OrganizationEntity? Organization { get; set; }

    public List<PipelineCacheEntity> Pipelines { get; set; } = new();
}
