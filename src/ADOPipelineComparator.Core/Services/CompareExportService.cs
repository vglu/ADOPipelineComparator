using System.Text;
using ADOPipelineComparator.Core.Interfaces;
using ADOPipelineComparator.Core.Models;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ADOPipelineComparator.Core.Services;

public sealed class CompareExportService : ICompareExportService
{
    public byte[] ExportExcel(CompareResult result, IReadOnlyList<PipelineCacheRecord> pipelines, string? sectionTitle = null)
    {
        using var workbook = new XLWorkbook();

        var sections = string.IsNullOrWhiteSpace(sectionTitle)
            ? result.Sections
            : result.Sections.Where(x => string.Equals(x.Title, sectionTitle, StringComparison.OrdinalIgnoreCase)).ToList();

        if (sections.Count == 0)
        {
            sections = new List<CompareSectionResult>
            {
                new()
                {
                    Title = "Compare",
                },
            };
        }

        foreach (var section in sections)
        {
            var worksheet = workbook.Worksheets.Add(SanitizeWorksheetName(section.Title));
            FillSectionWorksheet(worksheet, section, pipelines);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportPdf(CompareResult result, IReadOnlyList<PipelineCacheRecord> pipelines)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var selected = string.Join(", ", pipelines.Select(x => x.PipelineName));

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);

                page.Header().Column(column =>
                {
                    column.Item().Text("Pipeline Comparison Report").SemiBold().FontSize(18);
                    column.Item().Text($"Generated: {DateTime.UtcNow:u}").FontSize(10);
                    column.Item().Text($"Pipelines: {selected}").FontSize(10);
                });

                page.Content().Column(column =>
                {
                    foreach (var section in result.Sections)
                    {
                        column.Item().PaddingTop(12).Text(section.Title).SemiBold().FontSize(12);

                        if (section.Rows.Count == 0)
                        {
                            column.Item().Text("No rows in this section.").FontSize(9);
                            continue;
                        }

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                foreach (var _ in pipelines)
                                {
                                    columns.RelativeColumn(2);
                                }
                                columns.RelativeColumn(1);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("Key");
                                foreach (var pipeline in pipelines)
                                {
                                    header.Cell().Element(HeaderCell).Text(pipeline.PipelineName);
                                }
                                header.Cell().Element(HeaderCell).Text("Status");
                            });

                            foreach (var row in section.Rows)
                            {
                                table.Cell().Element(c => DataCell(c, row.Status)).Text(row.Key);

                                foreach (var pipeline in pipelines)
                                {
                                    var value = row.ValuesByPipelineId.TryGetValue(pipeline.Id, out var rowValue)
                                        ? NormalizeMultiline(rowValue)
                                        : "-";

                                    table.Cell().Element(c => DataCell(c, row.Status)).Text(value);
                                }

                                table.Cell().Element(c => DataCell(c, row.Status)).Text(row.Status.ToString());
                            }
                        });
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    public string ExportSectionCsv(CompareSectionResult section, IReadOnlyList<PipelineCacheRecord> pipelines)
    {
        var builder = new StringBuilder();

        var headers = new List<string> { "Key" };
        headers.AddRange(pipelines.Select(x => x.PipelineName));
        headers.Add("Status");

        builder.AppendLine(string.Join(',', headers.Select(EscapeCsv)));

        foreach (var row in section.Rows)
        {
            var columns = new List<string> { row.Key };
            columns.AddRange(pipelines.Select(pipeline =>
            {
                return row.ValuesByPipelineId.TryGetValue(pipeline.Id, out var value)
                    ? NormalizeMultiline(value)
                    : "";
            }));
            columns.Add(row.Status.ToString());

            builder.AppendLine(string.Join(',', columns.Select(EscapeCsv)));
        }

        return builder.ToString();
    }

    private static void FillSectionWorksheet(IXLWorksheet worksheet, CompareSectionResult section, IReadOnlyList<PipelineCacheRecord> pipelines)
    {
        worksheet.Cell(1, 1).Value = "Key";

        var column = 2;
        foreach (var pipeline in pipelines)
        {
            worksheet.Cell(1, column).Value = pipeline.PipelineName;
            column++;
        }

        worksheet.Cell(1, column).Value = "Status";

        var rowNumber = 2;
        foreach (var row in section.Rows)
        {
            worksheet.Cell(rowNumber, 1).Value = row.Key;

            column = 2;
            foreach (var pipeline in pipelines)
            {
                var value = row.ValuesByPipelineId.TryGetValue(pipeline.Id, out var rowValue)
                    ? NormalizeMultiline(rowValue)
                    : "";

                worksheet.Cell(rowNumber, column).Value = value;
                worksheet.Cell(rowNumber, column).Style.Alignment.WrapText = true;
                column++;
            }

            worksheet.Cell(rowNumber, column).Value = row.Status.ToString();

            var rowRange = worksheet.Range(rowNumber, 1, rowNumber, column);
            rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml(GetStatusColor(row.Status));
            rowRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

            rowNumber++;
        }

        var headerRange = worksheet.Range(1, 1, 1, column);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        worksheet.Columns().AdjustToContents();
        worksheet.SheetView.FreezeRows(1);
    }

    private static string EscapeCsv(string? value)
    {
        var text = value ?? string.Empty;
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        text = text.Replace("\r", "\n", StringComparison.Ordinal);

        if (text.Contains(',') || text.Contains('"') || text.Contains('\n'))
        {
            text = '"' + text.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
        }

        return text;
    }

    private static string NormalizeMultiline(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        return value
            .Replace("\\\\n", "\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string SanitizeWorksheetName(string title)
    {
        var safe = new string(title.Where(ch => !"[]:*?/\\".Contains(ch)).ToArray());
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "Sheet";
        }

        return safe.Length <= 31 ? safe : safe[..31];
    }

    private static string GetStatusColor(CompareRowStatus status)
    {
        return status switch
        {
            CompareRowStatus.Match => "#e8f5e9",
            CompareRowStatus.Different => "#fff9c4",
            CompareRowStatus.Missing => "#ffebee",
            _ => "#ffffff",
        };
    }

    private static IContainer HeaderCell(IContainer container)
    {
        return container
            .Border(1)
            .Padding(4)
            .Background("#eeeeee");
    }

    private static IContainer DataCell(IContainer container, CompareRowStatus status)
    {
        return container
            .Border(1)
            .Padding(4)
            .Background(GetStatusColor(status));
    }
}
