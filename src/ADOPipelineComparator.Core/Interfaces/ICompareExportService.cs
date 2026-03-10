using ADOPipelineComparator.Core.Models;

namespace ADOPipelineComparator.Core.Interfaces;

public interface ICompareExportService
{
    byte[] ExportExcel(CompareResult result, IReadOnlyList<PipelineCacheRecord> pipelines, string? sectionTitle = null);

    byte[] ExportPdf(CompareResult result, IReadOnlyList<PipelineCacheRecord> pipelines);

    string ExportSectionCsv(CompareSectionResult section, IReadOnlyList<PipelineCacheRecord> pipelines);
}
