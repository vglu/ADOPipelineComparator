using ADOPipelineComparator.Core.Models;
using ADOPipelineComparator.Core.Services;

namespace ADOPipelineComparator.Tests;

public sealed class CompareExportServiceTests
{
    [Fact]
    public void ExportExcel_ReturnsXlsxPayload()
    {
        var service = new CompareExportService();
        var (result, pipelines) = CreateFixture();

        var bytes = service.ExportExcel(result, pipelines);

        Assert.True(bytes.Length > 0);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public void ExportPdf_ReturnsPdfPayload()
    {
        var service = new CompareExportService();
        var (result, pipelines) = CreateFixture();

        var bytes = service.ExportPdf(result, pipelines);

        Assert.True(bytes.Length > 0);
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }

    [Fact]
    public void ExportSectionCsv_ContainsHeaderAndRows()
    {
        var service = new CompareExportService();
        var (result, pipelines) = CreateFixture();

        var csv = service.ExportSectionCsv(result.Sections[0], pipelines);

        Assert.Contains("Key", csv, StringComparison.Ordinal);
        Assert.Contains("Status", csv, StringComparison.Ordinal);
        Assert.Contains("AgentPool", csv, StringComparison.Ordinal);
    }

    private static (CompareResult Result, List<PipelineCacheRecord> Pipelines) CreateFixture()
    {
        var pipelines = new List<PipelineCacheRecord>
        {
            new()
            {
                Id = 1,
                PipelineName = "Pipeline One",
            },
            new()
            {
                Id = 2,
                PipelineName = "Pipeline Two",
            },
        };

        var result = new CompareResult
        {
            Sections =
            {
                new CompareSectionResult
                {
                    Title = "Variables",
                    Rows =
                    {
                        new CompareSectionRow
                        {
                            Key = "AgentPool",
                            Status = CompareRowStatus.Different,
                            ValuesByPipelineId = new Dictionary<int, string?>
                            {
                                [1] = "ubuntu-latest",
                                [2] = "windows-latest",
                            },
                        },
                    },
                },
            },
        };

        return (result, pipelines);
    }
}
