namespace ADOPipelineComparator.Core.Models;

public sealed class PipelineStepRecord
{
    public string Key { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Parameters { get; set; } = string.Empty;
}

public sealed class PipelineDefinitionRecord
{
    public PipelineCacheRecord Pipeline { get; set; } = new();

    public Dictionary<string, string?> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string?> Triggers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> VariableGroups { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<PipelineStepRecord> Steps { get; set; } = new();

    public List<string> Errors { get; set; } = new();
}
