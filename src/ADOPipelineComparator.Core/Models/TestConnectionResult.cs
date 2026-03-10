namespace ADOPipelineComparator.Core.Models;

public sealed class TestConnectionResult
{
    public bool IsSuccess { get; set; }

    public string Message { get; set; } = string.Empty;

    public TimeSpan Duration { get; set; }
}
