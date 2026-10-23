using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BaGet.Tests;

/// <summary>
/// The standing UI-coverage audit (docs/ui-coverage.md): a controller, Razor page or CLI command added without a row in the
/// capability table fails here, so "every operation has a UI counterpart" cannot rot silently. The administration REST API is
/// audited operation by operation by <c>AdminApiAccessTests</c>.
/// </summary>
public class UiCoverageInventoryTests : IDisposable
{
    private readonly BaGetApplication _app;

    public UiCoverageInventoryTests(ITestOutputHelper output)
    {
        _app = new BaGetApplication(
            output,
            configOverrides: new Dictionary<string, string>
            {
                { "Accounts:Enabled", "true" },
                { "Validation:Enabled", "true" },
                { "Feeds:EnableV2", "true" },
            });
    }

    public void Dispose() => _app.Dispose();

    /// <summary>
    /// Each protocol controller and the text of docs/ui-coverage.md that names the capability it serves and its page.
    /// Controllers of <c>/api/v1</c> are audited by <c>AdminApiAccessTests</c>.
    /// </summary>
    private static readonly Dictionary<string, string> ControllerRows = new()
    {
        ["Catalog"] = "Catalog index, pages, leaves",
        ["Maven"] = "Read file, checksum, `maven-metadata.xml`",
        ["Npm"] = "Publish",
        ["Oci"] = "Pull: blobs (ranges), manifests, tag list, referrers",
        ["PackageContent"] = "Package page, versions, dependents, readme, icon, download",
        ["PackageMetadata"] = "Registration, flat container, `.nuspec`",
        ["PackageMetadataAdmin"] = "Deprecation (reasons, message, alternate ID and version range)",
        ["PackagePublish"] = "Unlist / relist / delete",
        ["RepositorySignatures"] = "Repository signatures, certificate",
        ["Search"] = "Search, autocomplete",
        ["ServiceIndex"] = "Service index `v3/index.json`",
        ["Symbol"] = "Symbol download",
        ["V2Feed"] = "V2 feed",
        ["ValidationStatus"] = "Validation status of a submission",
        ["Vulnerability"] = "Vulnerability index",
    };

    /// <summary>Pages with no capability of their own: sign-in and legal pages, covered by the "Register, confirm email, sign in" row.</summary>
    private static readonly HashSet<string> AccountPages = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Account/ConfirmEmail", "/Account/ForgotPassword", "/Account/Login", "/Account/LoginWith2fa", "/Account/LoginWithRecoveryCode",
        "/Account/Logout", "/Account/Reauthenticate", "/Account/Register", "/Account/ResetPassword", "/Account/Profile",
        "/Admin/AccessUnavailable", "/Privacy", "/Terms",
    };

    private static string Docs => File.ReadAllText(Path.Combine(AdminApiAccessTests.RepositoryRoot(), "docs", "ui-coverage.md"));

    [Fact]
    public void EveryProtocolControllerHasARowInTheCoverageTable()
    {
        var provider = _app.Services.GetRequiredService<IActionDescriptorCollectionProvider>();
        var controllers = provider.ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(d => d.AttributeRouteInfo?.Template?.StartsWith("api/v1", StringComparison.Ordinal) != true)
            .Select(d => d.ControllerName)
            .Distinct()
            .ToList();
        Assert.NotEmpty(controllers);

        var docs = Docs;
        foreach (var controller in controllers)
        {
            Assert.True(ControllerRows.TryGetValue(controller, out var row), $"{controller}Controller has no row mapped in UiCoverageInventoryTests.ControllerRows: add its capability to docs/ui-coverage.md and here.");
            Assert.True(docs.Contains(row!, StringComparison.Ordinal), $"docs/ui-coverage.md has no row \"{row}\" for {controller}Controller.");
        }

        foreach (var mapped in ControllerRows.Keys) Assert.Contains(mapped, controllers);
    }

    /// <summary>Pages the table names by a phrase, not by class name or URL segment.</summary>
    private static readonly Dictionary<string, string> PagesNamedByText = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/Index"] = "Home and `/` search",
        ["/Account/ApiKeys"] = "Account API keys",
    };

    [Fact]
    public void EveryRazorPageIsNamedInTheCoverageTable()
    {
        var pages = _app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => (Page: e.Metadata.GetMetadata<CompiledPageActionDescriptor>(), Template: e.RoutePattern.RawText ?? string.Empty))
            .Where(p => p.Page != null)
            .GroupBy(p => p.Page!.ViewEnginePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Template).ToList(), StringComparer.OrdinalIgnoreCase);
        Assert.True(pages.Count > 50);

        var docs = Docs;
        bool Named(string page, IEnumerable<string> templates)
        {
            if (AccountPages.Contains(page)) return true;
            if (PagesNamedByText.TryGetValue(page, out var phrase)) return docs.Contains(phrase, StringComparison.Ordinal);
            if (Regex.IsMatch(docs, $@"\b{Regex.Escape(page.Substring(page.LastIndexOf('/') + 1))}\b")) return true;

            // By URL: the last literal segment of a route (`docker-images`, `report-abuse`, `validation`).
            return templates.Any(t =>
            {
                var literal = t.Split('/').LastOrDefault(segment => segment.Length > 0 && !segment.StartsWith('{') && !segment.StartsWith('@'));
                return literal != null && literal != "Index" && docs.Contains(literal, StringComparison.OrdinalIgnoreCase);
            });
        }

        var missing = pages.Where(p => !Named(p.Key, p.Value)).Select(p => p.Key).ToList();

        Assert.True(missing.Count == 0, "Pages missing from docs/ui-coverage.md: " + string.Join(", ", missing));
    }

    [Fact]
    public void EveryCommandLineCommandIsListedWithItsPage()
    {
        var program = File.ReadAllText(Path.Combine(AdminApiAccessTests.RepositoryRoot(), "src", "BaGet", "Program.cs"));
        var commands = Regex.Matches(program, @"\.Command\(""([a-z-]+)""").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.Contains("rebuild", commands);

        var docs = Docs;
        foreach (var command in commands)
        {
            Assert.True(Regex.IsMatch(docs, $@"CLI `[^`]*\b{Regex.Escape(command)}\b"), $"docs/ui-coverage.md has no CLI row for `{command}`.");
        }
    }
}
