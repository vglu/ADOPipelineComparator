namespace ADOPipelineComparator.Data.Entities;

public sealed class OrganizationEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string OrganizationUrl { get; set; } = string.Empty;

    public string Pat { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<AdoSiteEntity> Sites { get; set; } = new();
}
