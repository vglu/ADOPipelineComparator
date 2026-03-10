using Microsoft.Playwright;

namespace ADOPipelineComparator.E2E;

[Collection("ui-e2e")]
public sealed class UiFormsAndButtonsTests
{
    private readonly UiE2EFixture _fixture;

    public UiFormsAndButtonsTests(UiE2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FormsAndButtons_AreInteractive_WithScreenshots()
    {
        await using var context = await _fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1440, Height = 960 },
        });

        var page = await context.NewPageAsync();

        // --- 01: Home ---
        await page.GotoAsync(_fixture.BaseUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForTimeoutAsync(700); // allow Blazor circuit to initialize
        await _fixture.SaveScreenshotAsync(page, "01-home");

        await AssertEnabledAsync(Button(page, "qa-home-manage-sites-btn"));
        await AssertEnabledAsync(Button(page, "qa-home-browse-pipelines-btn"));

        // --- 02: Sites page (empty form) ---
        await Button(page, "qa-home-manage-sites-btn").ClickAsync();
        await page.WaitForURLAsync("**/sites");
        await page.WaitForSelectorAsync(".qa-site-name-input input", new PageWaitForSelectorOptions { Timeout = 10000 });
        await page.WaitForTimeoutAsync(300);
        await _fixture.SaveScreenshotAsync(page, "02-sites-empty-form");

        // --- Fill the Add Site form ---
        var nameInput = page.Locator(".qa-site-name-input input");
        var urlInput = page.Locator(".qa-site-url-input input");
        var patInput = page.Locator(".qa-site-pat-input input");

        // FillAsync fires one `input` event for the whole value — reliable with Blazor oninput
        await nameInput.FillAsync("UI E2E Site");
        await page.WaitForTimeoutAsync(400); // SignalR round-trip

        await urlInput.FillAsync("https://dev.azure.com/ui-e2e-org");
        await page.WaitForTimeoutAsync(400);

        await patInput.FillAsync("ui-e2e-pat");
        await page.WaitForTimeoutAsync(400);

        // --- 03: Form filled ---
        await _fixture.SaveScreenshotAsync(page, "03-sites-filled-form");

        Assert.Equal("UI E2E Site", await nameInput.InputValueAsync());
        Assert.Equal("https://dev.azure.com/ui-e2e-org", await urlInput.InputValueAsync());

        // --- Click Add ---
        var addButton = Button(page, "qa-site-save-btn");
        await AssertEnabledAsync(addButton);
        await addButton.ClickAsync();

        // Wait for "Site created." snackbar — Blazor fires snackbar after SaveAsync
        await page.WaitForSelectorAsync("text=Site created.", new PageWaitForSelectorOptions { Timeout = 15000 });
        await page.WaitForTimeoutAsync(500); // let LoadAsync re-render the table

        // --- 04: Sites table shows new row ---
        await _fixture.SaveScreenshotAsync(page, "04-sites-after-add");

        await page.WaitForSelectorAsync(".qa-site-edit-btn", new PageWaitForSelectorOptions { Timeout = 5000 });
        var tableText = await page.Locator(".qa-sites-table").InnerTextAsync();
        Assert.Contains("UI E2E Site", tableText, StringComparison.Ordinal);

        // --- 05: Edit mode ---
        var editButton = Button(page, "qa-site-edit-btn");
        await AssertEnabledAsync(editButton);
        await editButton.ClickAsync();
        await page.WaitForTimeoutAsync(400);
        await _fixture.SaveScreenshotAsync(page, "05-sites-edit-mode");

        // --- 06: After cancel ---
        var cancelButton = Button(page, "qa-site-cancel-btn");
        await AssertEnabledAsync(cancelButton);
        await cancelButton.ClickAsync();
        await page.WaitForTimeoutAsync(300);
        await _fixture.SaveScreenshotAsync(page, "06-sites-after-cancel");

        // --- 07: Test connection (will fail — no real ADO, but shows result snackbar) ---
        var testButton = Button(page, "qa-site-test-btn");
        await AssertEnabledAsync(testButton);
        await testButton.ClickAsync();
        await page.WaitForTimeoutAsync(3000); // network attempt + timeout
        await _fixture.SaveScreenshotAsync(page, "07-sites-after-test-connection");

        // --- 08: Delete dialog ---
        var deleteButton = Button(page, "qa-site-delete-btn");
        await AssertEnabledAsync(deleteButton);
        await deleteButton.ClickAsync();
        await page.WaitForSelectorAsync(".mud-dialog", new PageWaitForSelectorOptions { Timeout = 5000 });
        await _fixture.SaveScreenshotAsync(page, "08-sites-delete-dialog");

        // Dismiss dialog
        await page.ClickAsync(".mud-dialog button:has-text('Cancel')");
        await page.WaitForTimeoutAsync(300);

        // --- 09: Pipelines page ---
        await page.GotoAsync(_fixture.BaseUrl + "/pipelines");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForTimeoutAsync(700);
        await _fixture.SaveScreenshotAsync(page, "09-pipelines-initial");

        await AssertEnabledAsync(Button(page, "qa-pipelines-refresh-all-btn"));
        await AssertEnabledAsync(Button(page, "qa-pipelines-reload-cache-btn"));

        // --- 10: Reload cache ---
        await Button(page, "qa-pipelines-reload-cache-btn").ClickAsync();
        await page.WaitForTimeoutAsync(800);
        await _fixture.SaveScreenshotAsync(page, "10-pipelines-after-reload-cache");

        // --- 11: Refresh all (calls ADO — will show error for fake org, but button works) ---
        await Button(page, "qa-pipelines-refresh-all-btn").ClickAsync();
        await page.WaitForTimeoutAsync(3000);
        await _fixture.SaveScreenshotAsync(page, "11-pipelines-after-refresh-all");

        // --- 12: Search filter ---
        var searchInput = page.Locator(".qa-pipelines-search-input input");
        await searchInput.FillAsync("E2E");
        await page.WaitForTimeoutAsync(300);
        await _fixture.SaveScreenshotAsync(page, "12-pipelines-search");

        // --- 13: Compare page (empty state) ---
        await page.GotoAsync(_fixture.BaseUrl + "/compare");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForTimeoutAsync(700);
        await _fixture.SaveScreenshotAsync(page, "13-compare-empty-state");

        var compareAlert = page.Locator(".mud-alert");
        Assert.True(await compareAlert.CountAsync() > 0, "Compare page should show an alert when no pipelines are selected.");
    }

    private static async Task AssertEnabledAsync(ILocator locator)
    {
        Assert.True(await locator.CountAsync() > 0, "Element not found in DOM.");
        Assert.False(await locator.First.IsDisabledAsync(), "Element is disabled.");
    }

    private static ILocator Button(IPage page, string qaClass)
    {
        return page.Locator($"button.{qaClass}, a.{qaClass}, .{qaClass} button, .{qaClass} a").First;
    }
}
