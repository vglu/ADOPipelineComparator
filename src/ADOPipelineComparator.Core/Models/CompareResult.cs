namespace ADOPipelineComparator.Core.Models;

public enum CompareRowStatus
{
    Match = 1,
    Different = 2,
    Missing = 3,
}

public sealed class CompareSectionRow
{
    public string Key { get; set; } = string.Empty;

    public Dictionary<int, string?> ValuesByPipelineId { get; set; } = new();

    public CompareRowStatus Status { get; set; }
}

public sealed class CompareSectionResult
{
    public string Title { get; set; } = string.Empty;

    public List<CompareSectionRow> Rows { get; set; } = new();
}

public sealed class CompareResult
{
    public List<CompareSectionResult> Sections { get; set; } = new();
}
