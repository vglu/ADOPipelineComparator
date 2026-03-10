using System.ComponentModel.DataAnnotations;

namespace ADOPipelineComparator.Core.Models;

public sealed class OrganizationUpsertModel
{
    public int? Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(300)]
    [RegularExpression("^https?://.+", ErrorMessage = "Organization URL must start with http:// or https://")]
    public string OrganizationUrl { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Pat { get; set; }

    public bool IsActive { get; set; } = true;
}
