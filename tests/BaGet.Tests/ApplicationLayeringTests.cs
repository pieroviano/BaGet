using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BaGet.Tests.Contracts;
using Xunit;

namespace BaGet.Tests;

/// <summary>
/// The layering of the UI API split (decision D3): the Application projects hold use cases, DTOs and business services and reference no
/// MVC or Razor assembly; the UI projects hold controllers, views and MVC infrastructure, and the logic of a screen is not in a class named
/// <c>*Workflow</c> any more once its track ran.
/// </summary>
public class ApplicationLayeringTests
{
    private static readonly Assembly[] ApplicationAssemblies =
    {
        typeof(BaGet.Identity.Application.ScreenGate).Assembly,
        typeof(BaGet.Web.Application.LegalUseCase).Assembly,
    };

    private static readonly Assembly[] UiAssemblies =
    {
        typeof(BaGet.Identity.Mvc.UiControllerBase).Assembly,
        typeof(BaGet.Web.NavLinkTagHelper).Assembly,
    };

    [Fact]
    public void TheApplicationAssembliesReferenceNoMvcOrRazorAssembly()
    {
        foreach (var assembly in ApplicationAssemblies)
        {
            var forbidden = assembly.GetReferencedAssemblies()
                .Select(a => a.Name!)
                .Where(n => n.StartsWith("Microsoft.AspNetCore.Mvc", StringComparison.Ordinal)
                            || n.StartsWith("Microsoft.AspNetCore.Razor", StringComparison.Ordinal)
                            || n.StartsWith("Microsoft.AspNetCore.Components", StringComparison.Ordinal)
                            || n.StartsWith("BaGet.Web.V3", StringComparison.Ordinal)
                            || n == "BaGet.Identity")
                .ToList();

            Assert.True(forbidden.Count == 0, $"{assembly.GetName().Name} references {string.Join(", ", forbidden)}: the Application projects must stay free of MVC, Razor and the UI projects.");
        }
    }

    [Fact]
    public void TheApplicationProjectsDoNotReferenceTheUiProjectsInTheirProjectFiles()
    {
        var root = AdminApiAccessTests.RepositoryRoot();
        foreach (var project in new[] { "BaGet.Identity.Application", "BaGet.Web.Application" })
        {
            var text = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "src", project, project + ".csproj"));

            Assert.DoesNotContain("BaGet.Identity.csproj", text, StringComparison.Ordinal);
            Assert.DoesNotContain("BaGet.Web.V3.csproj", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Sdk=\"Microsoft.NET.Sdk.Razor\"", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NoNewWorkflowClassAppearsInTheUiProjects()
    {
        var workflows = UiAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Name.EndsWith("Workflow", StringComparison.Ordinal) && t.DeclaringType == null)
            .Select(t => t.FullName!)
            .ToList();

        UiApiPending.VerifyListed(UiApiPending.WorkflowsFile, workflows, "Workflow classes in the UI projects");
    }

    [Fact]
    public void TheUseCasesLiveInTheApplicationProjects()
    {
        var useCases = ApplicationAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Name.EndsWith("UseCase", StringComparison.Ordinal))
            .Select(t => t.FullName!)
            .ToList();

        Assert.Contains("BaGet.Web.Application.LegalUseCase", useCases);
        Assert.Contains("BaGet.Web.Application.StatisticsUseCase", useCases);
        Assert.DoesNotContain(UiAssemblies.SelectMany(a => a.GetTypes()), t => t.Name.EndsWith("UseCase", StringComparison.Ordinal));
    }
}
