using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ADOPipelineComparator.Core.Interfaces;
using ADOPipelineComparator.Core.Models;

namespace ADOPipelineComparator.Core.Services;

public sealed class AdoService : IAdoService
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly IOrganizationRepository _organizationRepository;
    private readonly IAdoSiteRepository _siteRepository;
    private readonly IPipelineCacheRepository _cacheRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly IHttpClientFactory _httpClientFactory;

    public AdoService(
        IOrganizationRepository organizationRepository,
        IAdoSiteRepository siteRepository,
        IPipelineCacheRepository cacheRepository,
        IEncryptionService encryptionService,
        IHttpClientFactory httpClientFactory)
    {
        _organizationRepository = organizationRepository;
        _siteRepository = siteRepository;
        _cacheRepository = cacheRepository;
        _encryptionService = encryptionService;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<TestConnectionResult> TestConnectionAsync(OrganizationRecord org, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var client = CreateAdoClient(org.OrganizationUrl, org.EncryptedPat);
            var orgUrl = NormalizeUrl(org.OrganizationUrl);
            using var response = await client.GetAsync($"{orgUrl}/_apis/projects?$top=1&api-version=7.1-preview.4", cancellationToken);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                return new TestConnectionResult
                {
                    IsSuccess = false,
                    Message = $"ADO returned {(int)response.StatusCode} {response.ReasonPhrase}.",
                    Duration = stopwatch.Elapsed,
                };
            }

            return new TestConnectionResult
            {
                IsSuccess = true,
                Message = "Connection succeeded.",
                Duration = stopwatch.Elapsed,
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new TestConnectionResult
            {
                IsSuccess = false,
                Message = ex.Message,
                Duration = stopwatch.Elapsed,
            };
        }
    }

    public Task<List<PipelineCacheRecord>> GetCachedPipelinesAsync(CancellationToken cancellationToken = default)
    {
        return _cacheRepository.GetAllAsync(cancellationToken);
    }

    public async Task<(List<AdoSiteViewModel> Sites, List<string> Errors)> SyncSitesFromAdoAsync(
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var sites = new List<AdoSiteViewModel>();

        var org = await _organizationRepository.GetByIdAsync(organizationId, cancellationToken);
        if (org is null)
        {
            errors.Add($"Organization {organizationId} not found.");
            return (sites, errors);
        }

        try
        {
            using var client = CreateAdoClient(org.OrganizationUrl, org.EncryptedPat);
            var projectNames = await FetchProjectsAsync(client, org.OrganizationUrl, cancellationToken);

            var now = DateTime.UtcNow;
            foreach (var projectName in projectNames)
            {
                var siteRecord = new AdoSiteRecord
                {
                    OrganizationId = organizationId,
                    OrganizationName = org.Name,
                    OrganizationUrl = org.OrganizationUrl,
                    EncryptedPat = org.EncryptedPat,
                    ProjectName = projectName,
                    IsActive = false,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                };
                var saved = await _siteRepository.UpsertByProjectNameAsync(siteRecord, cancellationToken);
                sites.Add(new AdoSiteViewModel
                {
                    Id = saved.Id,
                    OrganizationId = saved.OrganizationId,
                    OrganizationName = saved.OrganizationName,
                    ProjectName = saved.ProjectName,
                    IsActive = saved.IsActive,
                    CreatedAtUtc = saved.CreatedAtUtc,
                    UpdatedAtUtc = saved.UpdatedAtUtc,
                });
            }
        }
        catch (Exception ex)
        {
            errors.Add($"[{org.Name}] Failed to sync sites: {ex.Message}");
        }

        return (sites, errors);
    }

    public async Task<RefreshSummary> RefreshAllPipelinesAsync(CancellationToken cancellationToken = default)
    {
        var summary = new RefreshSummary();
        var activeSites = await _siteRepository.GetActiveAsync(cancellationToken);

        foreach (var site in activeSites)
        {
            var siteSummary = await RefreshSiteInternalAsync(site, cancellationToken);
            summary.Pipelines.AddRange(siteSummary.Pipelines);
            summary.Errors.AddRange(siteSummary.Errors);
        }

        return summary;
    }

    public async Task<RefreshSummary> RefreshPipelinesForSiteAsync(int adoSiteId, CancellationToken cancellationToken = default)
    {
        var site = await _siteRepository.GetByIdAsync(adoSiteId, cancellationToken);
        if (site is null)
        {
            return new RefreshSummary
            {
                Errors = { $"Site {adoSiteId} was not found." },
            };
        }

        return await RefreshSiteInternalAsync(site, cancellationToken);
    }

    public async Task<RefreshSummary> RefreshSinglePipelineAsync(int cacheRecordId, CancellationToken cancellationToken = default)
    {
        var summary = new RefreshSummary();
        var cached = await _cacheRepository.GetByIdAsync(cacheRecordId, cancellationToken);
        if (cached is null)
        {
            summary.Errors.Add($"Pipeline cache record {cacheRecordId} was not found.");
            return summary;
        }

        var site = await _siteRepository.GetByIdAsync(cached.AdoSiteId, cancellationToken);
        if (site is null)
        {
            summary.Errors.Add($"Site {cached.AdoSiteId} was not found.");
            return summary;
        }

        try
        {
            PipelineCacheRecord? refreshed;
            using var client = CreateAdoClient(site.OrganizationUrl, site.EncryptedPat);

            if (cached.PipelineType == PipelineType.Build)
            {
                refreshed = await FetchSingleBuildPipelineAsync(client, site, cached.PipelineId, cancellationToken);
            }
            else
            {
                refreshed = await FetchSingleReleasePipelineAsync(client, site, cached.PipelineId, cancellationToken);
            }

            if (refreshed is null)
            {
                summary.Errors.Add($"Pipeline {cached.PipelineId} in project {cached.Project} was not found in ADO.");
                return summary;
            }

            await _cacheRepository.UpsertAsync(refreshed, cancellationToken);
            summary.Pipelines.Add(refreshed);
            return summary;
        }
        catch (Exception ex)
        {
            summary.Errors.Add($"Failed to refresh pipeline {cached.PipelineId}: {ex.Message}");
            return summary;
        }
    }

    public async Task<List<PipelineDefinitionRecord>> GetPipelineDefinitionsAsync(
        IReadOnlyList<PipelineCacheRecord> pipelines,
        CancellationToken cancellationToken = default)
    {
        var definitions = new List<PipelineDefinitionRecord>();
        if (pipelines.Count == 0)
        {
            return definitions;
        }

        var sites = await _siteRepository.GetAllAsync(cancellationToken);
        var siteById = sites.ToDictionary(x => x.Id);

        foreach (var pipeline in pipelines)
        {
            if (!siteById.TryGetValue(pipeline.AdoSiteId, out var site))
            {
                definitions.Add(new PipelineDefinitionRecord
                {
                    Pipeline = pipeline,
                    Errors = { $"Site {pipeline.AdoSiteId} was not found." },
                });
                continue;
            }

            try
            {
                using var client = CreateAdoClient(site.OrganizationUrl, site.EncryptedPat);
                PipelineDefinitionRecord definition = pipeline.PipelineType == PipelineType.Build
                    ? await FetchBuildDefinitionForCompareAsync(client, site, pipeline, cancellationToken)
                    : await FetchReleaseDefinitionForCompareAsync(client, site, pipeline, cancellationToken);

                definitions.Add(definition);
            }
            catch (Exception ex)
            {
                definitions.Add(new PipelineDefinitionRecord
                {
                    Pipeline = pipeline,
                    Errors = { ex.Message },
                });
            }
        }

        return definitions;
    }

    private async Task<RefreshSummary> RefreshSiteInternalAsync(AdoSiteRecord site, CancellationToken cancellationToken)
    {
        var summary = new RefreshSummary();

        // Second threshold: cap total time spent on one site regardless of how many projects/pipelines it has.
        using var siteCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        siteCts.CancelAfter(TotalSiteTimeout);
        var siteToken = siteCts.Token;

        try
        {
            using var client = CreateAdoClient(site.OrganizationUrl, site.EncryptedPat);

            var builds = await FetchBuildPipelinesAsync(client, site, siteToken);
            summary.Pipelines.AddRange(builds);

            var releases = await FetchReleasePipelinesAsync(client, site, siteToken);
            summary.Pipelines.AddRange(releases);

            if (summary.Errors.Count == 0 || summary.Pipelines.Count > 0)
            {
                await _cacheRepository.UpsertManyAsync(site.Id, summary.Pipelines, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            summary.Errors.Add($"[{site.OrganizationName}/{site.ProjectName}] Refresh timed out after {TotalSiteTimeout.TotalMinutes:0} minutes.");
        }
        catch (Exception ex)
        {
            summary.Errors.Add($"[{site.OrganizationName}/{site.ProjectName}] {ex.Message}");
        }

        return summary;
    }

    private async Task<List<string>> FetchProjectsAsync(HttpClient client, string organizationUrl, CancellationToken cancellationToken)
    {
        var orgUrl = NormalizeUrl(organizationUrl);
        using var response = await client.GetAsync($"{orgUrl}/_apis/projects?api-version=7.1-preview.4", cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var projects = new List<string>();
        if (!document.RootElement.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return projects;
        }

        foreach (var item in values.EnumerateArray())
        {
            var projectName = GetString(item, "name");
            if (!string.IsNullOrWhiteSpace(projectName))
            {
                projects.Add(projectName);
            }
        }

        return projects;
    }

    private async Task<List<PipelineCacheRecord>> FetchBuildPipelinesAsync(
        HttpClient client,
        AdoSiteRecord site,
        CancellationToken cancellationToken)
    {
        var orgUrl = NormalizeUrl(site.OrganizationUrl);
        var encodedProject = Uri.EscapeDataString(site.ProjectName);
        var url = $"{orgUrl}/{encodedProject}/_apis/build/definitions?api-version=7.1-preview.7";
        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var records = new List<PipelineCacheRecord>();
        if (!document.RootElement.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return records;
        }

        foreach (var item in values.EnumerateArray())
        {
            var pipelineId = GetInt(item, "id");
            var pipelineName = GetString(item, "name") ?? string.Empty;

            var (lastRunDate, lastRunBy) = await FetchBuildLastRunAsync(client, orgUrl, site.ProjectName, pipelineId, cancellationToken);
            var subtype = ResolveBuildSubtype(item);

            records.Add(new PipelineCacheRecord
            {
                AdoSiteId = site.Id,
                OrganizationName = site.OrganizationName,
                Project = site.ProjectName,
                PipelineId = pipelineId,
                PipelineName = pipelineName,
                PipelineType = PipelineType.Build,
                PipelineSubtype = subtype,
                LastRunDateUtc = lastRunDate,
                LastRunBy = lastRunBy,
                PipelineUrl = $"{orgUrl}/{encodedProject}/_build?definitionId={pipelineId}",
                CachedAtUtc = DateTime.UtcNow,
            });
        }

        return records;
    }

    private async Task<List<PipelineCacheRecord>> FetchReleasePipelinesAsync(
        HttpClient client,
        AdoSiteRecord site,
        CancellationToken cancellationToken)
    {
        var orgUrl = NormalizeUrl(site.OrganizationUrl);
        var orgName = ExtractOrganizationName(orgUrl);
        var vsrmBase = $"https://vsrm.dev.azure.com/{orgName}";
        var encodedProject = Uri.EscapeDataString(site.ProjectName);

        var definitionsUrl = $"{vsrmBase}/{encodedProject}/_apis/release/definitions?api-version=7.1-preview.4";
        using var response = await client.GetAsync(definitionsUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var records = new List<PipelineCacheRecord>();
        if (!document.RootElement.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return records;
        }

        foreach (var item in values.EnumerateArray())
        {
            var pipelineId = GetInt(item, "id");
            var pipelineName = GetString(item, "name") ?? string.Empty;
            var (lastRunDate, lastRunBy) = await FetchReleaseLastRunAsync(client, vsrmBase, site.ProjectName, pipelineId, cancellationToken);

            records.Add(new PipelineCacheRecord
            {
                AdoSiteId = site.Id,
                OrganizationName = site.OrganizationName,
                Project = site.ProjectName,
                PipelineId = pipelineId,
                PipelineName = pipelineName,
                PipelineType = PipelineType.Release,
                PipelineSubtype = "ClassicRelease",
                LastRunDateUtc = lastRunDate,
                LastRunBy = lastRunBy,
                PipelineUrl = $"{orgUrl}/{encodedProject}/_release?definitionId={pipelineId}",
                CachedAtUtc = DateTime.UtcNow,
            });
        }

        return records;
    }

    private async Task<PipelineCacheRecord?> FetchSingleBuildPipelineAsync(
        HttpClient client,
        AdoSiteRecord site,
        int pipelineId,
        CancellationToken cancellationToken)
    {
        var orgUrl = NormalizeUrl(site.OrganizationUrl);
        var encodedProject = Uri.EscapeDataString(site.ProjectName);
        var url = $"{orgUrl}/{encodedProject}/_apis/build/definitions/{pipelineId}?api-version=7.1-preview.7";

        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        var (lastRunDate, lastRunBy) = await FetchBuildLastRunAsync(client, orgUrl, site.ProjectName, pipelineId, cancellationToken);
        var subtype = ResolveBuildSubtype(root);

        return new PipelineCacheRecord
        {
            AdoSiteId = site.Id,
            OrganizationName = site.OrganizationName,
            Project = site.ProjectName,
            PipelineId = pipelineId,
            PipelineName = GetString(root, "name") ?? $"Build {pipelineId}",
            PipelineType = PipelineType.Build,
            PipelineSubtype = subtype,
            LastRunDateUtc = lastRunDate,
            LastRunBy = lastRunBy,
            PipelineUrl = $"{orgUrl}/{encodedProject}/_build?definitionId={pipelineId}",
            CachedAtUtc = DateTime.UtcNow,
        };
    }

    private async Task<PipelineCacheRecord?> FetchSingleReleasePipelineAsync(
        HttpClient client,
        AdoSiteRecord site,
        int pipelineId,
        CancellationToken cancellationToken)
    {
        var orgUrl = NormalizeUrl(site.OrganizationUrl);
        var orgName = ExtractOrganizationName(orgUrl);
        var vsrmBase = $"https://vsrm.dev.azure.com/{orgName}";
        var encodedProject = Uri.EscapeDataString(site.ProjectName);

        var definitionUrl = $"{vsrmBase}/{encodedProject}/_apis/release/definitions/{pipelineId}?api-version=7.1-preview.4";
        using var response = await client.GetAsync(definitionUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        var (lastRunDate, lastRunBy) = await FetchReleaseLastRunAsync(client, vsrmBase, site.ProjectName, pipelineId, cancellationToken);

        return new PipelineCacheRecord
        {
            AdoSiteId = site.Id,
            OrganizationName = site.OrganizationName,
            Project = site.ProjectName,
            PipelineId = pipelineId,
            PipelineName = GetString(root, "name") ?? $"Release {pipelineId}",
            PipelineType = PipelineType.Release,
            PipelineSubtype = "ClassicRelease",
            LastRunDateUtc = lastRunDate,
            LastRunBy = lastRunBy,
            PipelineUrl = $"{orgUrl}/{encodedProject}/_release?definitionId={pipelineId}",
            CachedAtUtc = DateTime.UtcNow,
        };
    }

    private async Task<PipelineDefinitionRecord> FetchBuildDefinitionForCompareAsync(
        HttpClient client,
        AdoSiteRecord site,
        PipelineCacheRecord pipeline,
        CancellationToken cancellationToken)
    {
        var definition = new PipelineDefinitionRecord
        {
            Pipeline = pipeline,
        };

        var orgUrl = NormalizeUrl(site.OrganizationUrl);
        var encodedProject = Uri.EscapeDataString(pipeline.Project);
        var url = $"{orgUrl}/{encodedProject}/_apis/build/definitions/{pipeline.PipelineId}?api-version=7.1-preview.7";

        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            definition.Errors.Add($"Failed to fetch build definition: {(int)response.StatusCode} {response.ReasonPhrase}");
            return definition;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        ParseRootVariables(root, definition.Variables);
        ParseBuildTriggers(root, definition.Triggers);
        ParseVariableGroups(root, definition.VariableGroups);
        ParseBuildSteps(root, definition.Steps);

        return definition;
    }

    private async Task<PipelineDefinitionRecord> FetchReleaseDefinitionForCompareAsync(
        HttpClient client,
        AdoSiteRecord site,
        PipelineCacheRecord pipeline,
        CancellationToken cancellationToken)
    {
        var definition = new PipelineDefinitionRecord
        {
            Pipeline = pipeline,
        };

        var orgUrl = NormalizeUrl(site.OrganizationUrl);
        var orgName = ExtractOrganizationName(orgUrl);
        var encodedProject = Uri.EscapeDataString(pipeline.Project);
        var vsrmBase = $"https://vsrm.dev.azure.com/{orgName}";
        var url = $"{vsrmBase}/{encodedProject}/_apis/release/definitions/{pipeline.PipelineId}?api-version=7.1-preview.4";

        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            definition.Errors.Add($"Failed to fetch release definition: {(int)response.StatusCode} {response.ReasonPhrase}");
            return definition;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        ParseRootVariables(root, definition.Variables);
        ParseReleaseEnvironmentVariables(root, definition.Variables);
        ParseBuildTriggers(root, definition.Triggers);
        ParseVariableGroups(root, definition.VariableGroups);
        ParseReleaseSteps(root, definition.Steps);

        return definition;
    }

    private static void ParseRootVariables(JsonElement root, IDictionary<string, string?> output)
    {
        if (!root.TryGetProperty("variables", out var variables) || variables.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var variable in variables.EnumerateObject())
        {
            output[variable.Name] = ExtractVariableValue(variable.Value);
        }
    }

    private static void ParseReleaseEnvironmentVariables(JsonElement root, IDictionary<string, string?> output)
    {
        if (!root.TryGetProperty("environments", out var environments) || environments.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var environmentIndex = 0;
        foreach (var environment in environments.EnumerateArray())
        {
            environmentIndex++;
            var environmentName = GetString(environment, "name") ?? $"Environment {environmentIndex}";
            if (!environment.TryGetProperty("variables", out var variables) || variables.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var variable in variables.EnumerateObject())
            {
                output[$"{environmentName}::{variable.Name}"] = ExtractVariableValue(variable.Value);
            }
        }
    }

    private static string? ExtractVariableValue(JsonElement valueElement)
    {
        return valueElement.ValueKind switch
        {
            JsonValueKind.String => valueElement.GetString(),
            JsonValueKind.Number => valueElement.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Object => ExtractVariableObjectValue(valueElement),
            JsonValueKind.Array => SerializeJson(valueElement),
            _ => null,
        };
    }

    private static string? ExtractVariableObjectValue(JsonElement valueElement)
    {
        if (valueElement.TryGetProperty("value", out var valueProperty))
        {
            return valueProperty.ValueKind == JsonValueKind.String
                ? valueProperty.GetString()
                : valueProperty.ToString();
        }

        if (valueElement.TryGetProperty("isSecret", out var secretProperty)
            && secretProperty.ValueKind == JsonValueKind.True)
        {
            return "***";
        }

        return SerializeJson(valueElement);
    }

    private static void ParseBuildTriggers(JsonElement root, IDictionary<string, string?> output)
    {
        if (!root.TryGetProperty("triggers", out var triggers) || triggers.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var trigger in triggers.EnumerateArray())
        {
            index++;
            var triggerType = GetString(trigger, "triggerType") ?? $"Trigger {index}";
            output[$"{triggerType} [{index}]"] = SerializeJson(trigger);
        }
    }

    private static void ParseVariableGroups(JsonElement root, ISet<string> output)
    {
        CollectVariableGroups(root, output);
    }

    private static void CollectVariableGroups(JsonElement element, ISet<string> output)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, "variableGroups", StringComparison.OrdinalIgnoreCase)
                        && property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var group in property.Value.EnumerateArray())
                        {
                            var groupName = ExtractVariableGroupName(group);
                            if (!string.IsNullOrWhiteSpace(groupName))
                            {
                                output.Add(groupName);
                            }
                        }
                    }

                    CollectVariableGroups(property.Value, output);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectVariableGroups(item, output);
                }

                break;
        }
    }

    private static string? ExtractVariableGroupName(JsonElement group)
    {
        return group.ValueKind switch
        {
            JsonValueKind.String => group.GetString(),
            JsonValueKind.Number => group.ToString(),
            JsonValueKind.Object =>
                GetString(group, "name")
                ?? GetString(group, "id")
                ?? (group.TryGetProperty("id", out var id) ? id.ToString() : null),
            _ => null,
        };
    }

    private static void ParseBuildSteps(JsonElement root, ICollection<PipelineStepRecord> output)
    {
        if (root.TryGetProperty("process", out var process) && process.ValueKind == JsonValueKind.Object)
        {
            var yamlFilename = GetString(process, "yamlFilename");
            if (!string.IsNullOrWhiteSpace(yamlFilename))
            {
                output.Add(new PipelineStepRecord
                {
                    Key = "YAML / File",
                    Type = "yaml",
                    Name = "yamlFilename",
                    Parameters = yamlFilename,
                });
            }

            if (process.TryGetProperty("phases", out var phases) && phases.ValueKind == JsonValueKind.Array)
            {
                var phaseIndex = 0;
                foreach (var phase in phases.EnumerateArray())
                {
                    phaseIndex++;
                    var phaseName = GetString(phase, "name")
                        ?? GetString(phase, "refName")
                        ?? $"Phase {phaseIndex}";

                    if (!phase.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    var stepIndex = 0;
                    foreach (var step in steps.EnumerateArray())
                    {
                        stepIndex++;
                        output.Add(BuildStepRecord(step, $"Phase: {phaseName}", stepIndex));
                    }
                }
            }
        }

        if (root.TryGetProperty("steps", out var rootSteps) && rootSteps.ValueKind == JsonValueKind.Array)
        {
            var stepIndex = 0;
            foreach (var step in rootSteps.EnumerateArray())
            {
                stepIndex++;
                output.Add(BuildStepRecord(step, "Root", stepIndex));
            }
        }
    }

    private static void ParseReleaseSteps(JsonElement root, ICollection<PipelineStepRecord> output)
    {
        if (!root.TryGetProperty("environments", out var environments) || environments.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var environmentIndex = 0;
        foreach (var environment in environments.EnumerateArray())
        {
            environmentIndex++;
            var environmentName = GetString(environment, "name") ?? $"Environment {environmentIndex}";

            if (!environment.TryGetProperty("deployPhases", out var deployPhases) || deployPhases.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var phaseIndex = 0;
            foreach (var deployPhase in deployPhases.EnumerateArray())
            {
                phaseIndex++;
                var phaseName = GetString(deployPhase, "name")
                    ?? GetString(deployPhase, "phaseType")
                    ?? $"Phase {phaseIndex}";

                if (!deployPhase.TryGetProperty("workflowTasks", out var tasks) || tasks.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var taskIndex = 0;
                foreach (var task in tasks.EnumerateArray())
                {
                    taskIndex++;
                    output.Add(BuildStepRecord(task, $"Environment: {environmentName} / Phase: {phaseName}", taskIndex));
                }
            }
        }
    }

    private static PipelineStepRecord BuildStepRecord(JsonElement step, string scope, int order)
    {
        var type = GetNestedString(step, "taskDefinitionReference", "name")
            ?? GetString(step, "taskId")
            ?? (step.TryGetProperty("script", out var scriptProperty) && scriptProperty.ValueKind == JsonValueKind.String
                ? "script"
                : "step");

        var name = GetString(step, "displayName")
            ?? GetString(step, "name")
            ?? type;

        return new PipelineStepRecord
        {
            Key = $"{scope} / {order:000} / {name}",
            Type = type,
            Name = name,
            Parameters = ExtractStepParameters(step),
        };
    }

    private static string ExtractStepParameters(JsonElement step)
    {
        var parts = new List<string>();

        if (step.TryGetProperty("inputs", out var inputs) && inputs.ValueKind == JsonValueKind.Object)
        {
            parts.Add("inputs:\n" + SerializeJson(inputs));
        }

        if (step.TryGetProperty("script", out var script) && script.ValueKind == JsonValueKind.String)
        {
            parts.Add("script:\n" + script.GetString());
        }

        if (step.TryGetProperty("condition", out var condition) && condition.ValueKind == JsonValueKind.String)
        {
            parts.Add("condition: " + condition.GetString());
        }

        return parts.Count == 0
            ? SerializeJson(step)
            : string.Join("\n", parts);
    }

    private static string SerializeJson(JsonElement element)
    {
        return JsonSerializer.Serialize(element, IndentedJsonOptions);
    }

    private static async Task<(DateTime? LastRunDateUtc, string? LastRunBy)> FetchBuildLastRunAsync(
        HttpClient client,
        string orgUrl,
        string project,
        int pipelineId,
        CancellationToken cancellationToken)
    {
        var encodedProject = Uri.EscapeDataString(project);
        var url = $"{orgUrl}/{encodedProject}/_apis/build/builds?definitions={pipelineId}&$top=1&queryOrder=finishTimeDescending&api-version=7.1-preview.7";
        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return (null, null);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!TryGetFirstArrayItem(document.RootElement, out var first))
        {
            return (null, null);
        }

        var lastRunDate = GetDateTime(first, "finishTime")
            ?? GetDateTime(first, "startTime")
            ?? GetDateTime(first, "queueTime");

        var lastRunBy = GetNestedString(first, "requestedFor", "displayName");
        return (lastRunDate, lastRunBy);
    }

    private static async Task<(DateTime? LastRunDateUtc, string? LastRunBy)> FetchReleaseLastRunAsync(
        HttpClient client,
        string vsrmBase,
        string project,
        int pipelineId,
        CancellationToken cancellationToken)
    {
        var encodedProject = Uri.EscapeDataString(project);
        var url = $"{vsrmBase}/{encodedProject}/_apis/release/releases?definitionId={pipelineId}&$top=1&queryOrder=descending&api-version=7.1-preview.8";
        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return (null, null);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!TryGetFirstArrayItem(document.RootElement, out var first))
        {
            return (null, null);
        }

        var lastRunDate = GetDateTime(first, "modifiedOn")
            ?? GetDateTime(first, "createdOn");

        var lastRunBy = GetNestedString(first, "createdBy", "displayName");
        return (lastRunDate, lastRunBy);
    }

    // Per-request connect+read timeout, and overall cap for all requests to one site.
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TotalSiteTimeout = TimeSpan.FromMinutes(5);

    private HttpClient CreateAdoClient(string organizationUrl, string encryptedPat)
    {
        var client = _httpClientFactory.CreateClient();
        var pat = _encryptionService.Decrypt(encryptedPat);
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));

        // Double threshold: per-request timeout + total timeout ceiling.
        client.Timeout = RequestTimeout;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }

    private static string NormalizeUrl(string url)
    {
        return url.Trim().TrimEnd('/');
    }

    private static string ResolveBuildSubtype(JsonElement buildDefinition)
    {
        if (buildDefinition.TryGetProperty("process", out var process)
            && process.ValueKind == JsonValueKind.Object
            && process.TryGetProperty("yamlFilename", out var yaml)
            && yaml.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(yaml.GetString()))
        {
            return "YAML";
        }

        return "ClassicBuild";
    }

    private static string ExtractOrganizationName(string orgUrl)
    {
        var uri = new Uri(orgUrl, UriKind.Absolute);

        if (uri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            var hostParts = uri.Host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (hostParts.Length > 0)
            {
                return hostParts[0];
            }
        }

        var segment = uri.Segments.FirstOrDefault(x => x != "/");
        if (segment is null)
        {
            throw new InvalidOperationException($"Unable to parse organization name from URL '{orgUrl}'.");
        }

        return segment.Trim('/');
    }

    private static bool TryGetFirstArrayItem(JsonElement root, out JsonElement item)
    {
        item = default;

        if (!root.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var enumerator = value.EnumerateArray();
        if (!enumerator.MoveNext())
        {
            return false;
        }

        item = enumerator.Current;
        return true;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static int GetInt(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value))
        {
            return value;
        }

        return 0;
    }

    private static string? GetNestedString(JsonElement element, string objectName, string propertyName)
    {
        if (!element.TryGetProperty(objectName, out var nestedObject) || nestedObject.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetString(nestedObject, propertyName);
    }

    private static DateTime? GetDateTime(JsonElement element, string propertyName)
    {
        var raw = GetString(element, propertyName);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (DateTime.TryParse(raw, out var value))
        {
            return value.ToUniversalTime();
        }

        return null;
    }
}
