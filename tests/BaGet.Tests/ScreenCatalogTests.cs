using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BaGet.Core;
using BaGet.Frontend.Mvc.UI;
using BaGet.Identity.Application.Screens;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BaGet.Tests;

/// <summary>
/// A screen is its controller and default GET action (<see cref="ScreenRef"/>). Every name that is typed as text (the catalog, the links a format
/// module contributes) must resolve to an action that exists; the names written as <c>typeof</c> + <c>nameof</c> (<c>[UiApi]</c>,
/// <c>[ApiOperation]</c>) are checked by <c>UiApiParityTests</c> and <c>AdminApiAccessTests</c>.
/// </summary>
public class ScreenCatalogTests : IDisposable
{
    private readonly BaGetApplication _app;

    public ScreenCatalogTests(ITestOutputHelper output)
    {
        _app = new BaGetApplication(output, configOverrides: new Dictionary<string, string>
        {
            { "Accounts:Enabled", "true" },
            { "Validation:Enabled", "true" },
            { "Feeds:EnableV2", "true" },
        });
    }

    public void Dispose() => _app.Dispose();

    private HashSet<ScreenRef> Screens()
        => _app.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Select(a => ScreenEndpoints.ScreenOf(a))
            .Where(s => s != null)
            .Select(s => s!)
            .ToHashSet();

    [Fact]
    public void EveryCatalogEntryIsAScreenOfAViewController()
    {
        var screens = Screens();

        Assert.Empty(ScreenCatalog.All.Where(s => !screens.Contains(s)).Select(s => s.ToString()));
    }

    [Fact]
    public void EveryScreenOfAViewControllerIsInTheCatalog()
    {
        var catalog = ScreenCatalog.All.ToHashSet();

        Assert.Empty(Screens().Where(s => !catalog.Contains(s)).Select(s => s.ToString()));
    }

    [Fact]
    public void TheCatalogListsEveryMember()
    {
        var members = typeof(ScreenCatalog).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (ScreenRef)f.GetValue(null)!).ToHashSet();

        Assert.Equal(members.Count, ScreenCatalog.All.Count);
        Assert.True(members.SetEquals(ScreenCatalog.All));
    }

    [Fact]
    public void EveryLinkAFormatContributesOpensAScreen()
    {
        var screens = Screens();
        var registry = _app.Services.GetRequiredService<FormatRegistry>();
        var checkedLinks = 0;

        foreach (var module in registry.Modules)
        {
            foreach (var kind in new[] { RepositoryKind.Local, RepositoryKind.Remote, RepositoryKind.Virtual })
            {
                foreach (var link in module.RepositoryLinks(new RepositoryInfo(1, "x", module.Format, kind)).Where(l => l.Controller != null || l.Action != null))
                {
                    checkedLinks++;
                    Assert.True(screens.Contains(new ScreenRef(link.Controller!, link.Action!)), $"{module.Format} link {link.Id}: {link.Controller}/{link.Action} is no screen.");
                }
            }
        }

        Assert.True(checkedLinks > 0);
    }
}
