namespace ADOPipelineComparator.Core.Models;

public sealed class RefreshSummary
{
    public List<PipelineCacheRecord> Pipelines { get; } = new();

    public List<string> Errors { get; } = new();
}
