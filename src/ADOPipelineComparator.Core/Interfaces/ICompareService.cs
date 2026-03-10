using ADOPipelineComparator.Core.Models;

namespace ADOPipelineComparator.Core.Interfaces;

public interface ICompareService
{
    CompareResult Compare(IReadOnlyList<PipelineDefinitionRecord> definitions);
}
