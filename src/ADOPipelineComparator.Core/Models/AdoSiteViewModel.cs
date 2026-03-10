namespace ADOPipelineComparator.Core.Models;

public sealed class AdoSiteViewModel
{
    public int Id { get; set; }

    public int OrganizationId { get; set; }

    public string OrganizationName { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
