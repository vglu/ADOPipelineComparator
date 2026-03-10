using System.Diagnostics;
using System.Net.Http;
using Microsoft.Playwright;

namespace ADOPipelineComparator.E2E;

[CollectionDefinition("ui-e2e")]
public sealed class UiE2ECollection : ICollectionFixture<UiE2EFixture>
{
}

public sealed class UiE2EFixture : IAsyncLifetime
{
    private const string BaseAddress = "http://127.0.0.1:5099";

    private Process? _webProcess;
    private IPlaywright? _playwright;

    public string SolutionRoot { get; } = ResolveSolutionRoot();

    public string ScreenshotRoot { get; }

    public string BaseUrl => BaseAddress;

    public IBrowser Browser { get; private set; } = default!;

    public UiE2EFixture()
    {
        ScreenshotRoot = Path.Combine(SolutionRoot, "docs", "testing", "screenshots", "latest");
    }

    public async Task InitializeAsync()
    {
        if (Directory.Exists(ScreenshotRoot))
        {
            Directory.Delete(ScreenshotRoot, recursive: true);
        }

        Directory.CreateDirectory(ScreenshotRoot);

        await StartWebAppAsync();

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }

        _playwright?.Dispose();

        if (_webProcess is { HasExited: false })
        {
            _webProcess.Kill(entireProcessTree: true);
            await _webProcess.WaitForExitAsync();
        }
    }

    public Task SaveScreenshotAsync(IPage page, string name)
    {
        var fileName = string.Concat(name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '-' : ch));
        var path = Path.Combine(ScreenshotRoot, fileName + ".png");
        return page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = path,
            FullPage = true,
        });
    }

    private async Task StartWebAppAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ado-pipeline-comparator-e2e.db");
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }

        var logPath = Path.Combine(ScreenshotRoot, "web.log");
        var logWriter = new StreamWriter(logPath, append: false)
        {
            AutoFlush = true,
        };

        var startInfo = new ProcessStartInfo("dotnet", "run --no-launch-profile --project src/ADOPipelineComparator.Web/ADOPipelineComparator.Web.csproj")
        {
            WorkingDirectory = SolutionRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.Environment["ENCRYPTION_KEY"] = "0123456789ABCDEF0123456789ABCDEF";
        startInfo.Environment["DB_PATH"] = dbPath;
        startInfo.Environment["ASPNETCORE_URLS"] = BaseAddress;
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        _webProcess = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        _webProcess.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                logWriter.WriteLine(args.Data);
            }
        };

        _webProcess.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                logWriter.WriteLine(args.Data);
            }
        };

        if (!_webProcess.Start())
        {
            throw new InvalidOperationException("Failed to start web process for e2e tests.");
        }

        _webProcess.BeginOutputReadLine();
        _webProcess.BeginErrorReadLine();

        await WaitUntilReadyAsync();
    }

    private async Task WaitUntilReadyAsync()
    {
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3),
        };

        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            if (_webProcess is null || _webProcess.HasExited)
            {
                throw new InvalidOperationException("Web process exited before becoming ready.");
            }

            try
            {
                using var response = await client.GetAsync(BaseAddress);
                if ((int)response.StatusCode < 500)
                {
                    return;
                }
            }
            catch
            {
                // Ignore warm-up failures until the deadline.
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException("Timed out while waiting for the web application to become ready.");
    }

    private static string ResolveSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (directory.EnumerateFiles("*.sln").Any())
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to resolve solution root for e2e tests.");
    }
}
