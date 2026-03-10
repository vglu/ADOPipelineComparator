using ADOPipelineComparator.Core.Interfaces;
using ADOPipelineComparator.Core.Models;

namespace ADOPipelineComparator.Core.Services;

public sealed class CompareService : ICompareService
{
    public CompareResult Compare(IReadOnlyList<PipelineDefinitionRecord> definitions)
    {
        var result = new CompareResult();

        if (definitions.Count == 0)
        {
            return result;
        }

        result.Sections.Add(BuildVariablesSection(definitions));
        result.Sections.Add(BuildTriggersSection(definitions));
        result.Sections.Add(BuildVariableGroupsSection(definitions));
        result.Sections.Add(BuildStepsSection(definitions));
        result.Sections.Add(BuildErrorsSection(definitions));

        return result;
    }

    private static CompareSectionResult BuildVariablesSection(IReadOnlyList<PipelineDefinitionRecord> definitions)
    {
        var keys = definitions
            .SelectMany(x => x.Variables.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = keys.Select(key => BuildRow(key, definitions, (definition, rowKey) =>
        {
            definition.Variables.TryGetValue(rowKey, out var value);
            return value;
        })).ToList();

        return new CompareSectionResult
        {
            Title = "Variables",
            Rows = rows,
        };
    }

    private static CompareSectionResult BuildTriggersSection(IReadOnlyList<PipelineDefinitionRecord> definitions)
    {
        var keys = definitions
            .SelectMany(x => x.Triggers.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = keys.Select(key => BuildRow(key, definitions, (definition, rowKey) =>
        {
            definition.Triggers.TryGetValue(rowKey, out var value);
            return value;
        })).ToList();

        return new CompareSectionResult
        {
            Title = "Triggers",
            Rows = rows,
        };
    }

    private static CompareSectionResult BuildVariableGroupsSection(IReadOnlyList<PipelineDefinitionRecord> definitions)
    {
        var keys = definitions
            .SelectMany(x => x.VariableGroups)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = keys.Select(key => BuildRow(key, definitions, static (definition, rowKey) =>
        {
            return definition.VariableGroups.Contains(rowKey) ? "Yes" : null;
        })).ToList();

        return new CompareSectionResult
        {
            Title = "Variable Groups",
            Rows = rows,
        };
    }

    private static CompareSectionResult BuildStepsSection(IReadOnlyList<PipelineDefinitionRecord> definitions)
    {
        var valueMaps = definitions.ToDictionary(
            keySelector: x => x.Pipeline.Id,
            elementSelector: BuildStepMap);

        var keys = valueMaps
            .SelectMany(x => x.Value.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = keys.Select(key =>
        {
            var valuesByPipelineId = definitions.ToDictionary(
                keySelector: x => x.Pipeline.Id,
                elementSelector: definition =>
                {
                    var map = valueMaps[definition.Pipeline.Id];
                    return map.TryGetValue(key, out var value) ? value : null;
                });

            return new CompareSectionRow
            {
                Key = key,
                ValuesByPipelineId = valuesByPipelineId,
                Status = DetermineStatus(valuesByPipelineId.Values),
            };
        }).ToList();

        return new CompareSectionResult
        {
            Title = "Steps",
            Rows = rows,
        };
    }

    private static CompareSectionResult BuildErrorsSection(IReadOnlyList<PipelineDefinitionRecord> definitions)
    {
        var rows = new List<CompareSectionRow>();

        foreach (var definition in definitions)
        {
            if (definition.Errors.Count == 0)
            {
                continue;
            }

            var valuesByPipelineId = definitions.ToDictionary(
                keySelector: x => x.Pipeline.Id,
                elementSelector: x => x.Pipeline.Id == definition.Pipeline.Id
                    ? string.Join("\n", definition.Errors)
                    : null);

            rows.Add(new CompareSectionRow
            {
                Key = $"{definition.Pipeline.OrganizationName}/{definition.Pipeline.Project}/{definition.Pipeline.PipelineName}",
                ValuesByPipelineId = valuesByPipelineId,
                Status = CompareRowStatus.Missing,
            });
        }

        return new CompareSectionResult
        {
            Title = "Errors",
            Rows = rows,
        };
    }

    private static CompareSectionRow BuildRow(
        string key,
        IReadOnlyList<PipelineDefinitionRecord> definitions,
        Func<PipelineDefinitionRecord, string, string?> valueFactory)
    {
        var valuesByPipelineId = definitions.ToDictionary(
            keySelector: x => x.Pipeline.Id,
            elementSelector: definition => valueFactory(definition, key));

        return new CompareSectionRow
        {
            Key = key,
            ValuesByPipelineId = valuesByPipelineId,
            Status = DetermineStatus(valuesByPipelineId.Values),
        };
    }

    private static Dictionary<string, string?> BuildStepMap(PipelineDefinitionRecord definition)
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var step in definition.Steps)
        {
            var value = $"{step.Type}: {step.Name}";
            if (!string.IsNullOrWhiteSpace(step.Parameters))
            {
                value += "\n" + step.Parameters;
            }

            if (map.TryGetValue(step.Key, out var current) && !string.IsNullOrWhiteSpace(current))
            {
                map[step.Key] = current + "\n---\n" + value;
            }
            else
            {
                map[step.Key] = value;
            }
        }

        return map;
    }

    private static CompareRowStatus DetermineStatus(IEnumerable<string?> values)
    {
        var valueList = values.ToList();
        var nonEmpty = valueList.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        if (nonEmpty.Count != valueList.Count)
        {
            return CompareRowStatus.Missing;
        }

        if (nonEmpty.Select(Normalize).Distinct(StringComparer.Ordinal).Count() == 1)
        {
            return CompareRowStatus.Match;
        }

        return CompareRowStatus.Different;
    }

    private static string Normalize(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim();
    }
}
