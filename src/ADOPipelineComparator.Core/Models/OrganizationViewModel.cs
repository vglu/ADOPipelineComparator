namespace ADOPipelineComparator.Core.Models;

public sealed class OrganizationViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string OrganizationUrl { get; set; } = string.Empty;

    public string PatMasked { get; set; } = "********";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
