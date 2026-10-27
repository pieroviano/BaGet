using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Controllers.Base;
using Xunit;

namespace BaGet.Tests;

/// <summary>
/// The layering of the UI API split (decision D3): the Application projects hold use cases, DTOs and business services and reference no
/// MVC or Razor assembly; the UI projects hold controllers, views and MVC infrastructure, and the logic of a screen is not in a class named
/// <c>*Workflow</c> (none is left) and a view controller resolves no use case or Application service itself.
/// </summary>
public class ApplicationLayeringTests
{
    private static readonly Assembly[] ApplicationAssemblies =
    {
        typeof(ScreenGate).Assembly,
        typeof(BaGet.Web.Application.LegalUseCase).Assembly,
    };

    private static readonly Assembly[] UiAssemblies =
    {
        typeof(UiControllerBase).Assembly,
        typeof(BaGet.Web.V3.NavLinkTagHelper).Assembly,
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
    public void NoWorkflowClassIsLeftInTheUiProjects()
    {
        var workflows = UiAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Name.EndsWith("Workflow", StringComparison.Ordinal) && t.DeclaringType == null)
            .Select(t => t.FullName!)
            .ToList();

        Assert.True(workflows.Count == 0, "Workflow classes in the UI projects:" + Environment.NewLine + string.Join(Environment.NewLine, workflows));
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

    [Fact]
    public void AViewControllerHoldsNoUseCaseOrApplicationService()
    {
        // The view controller binds, calls the JSON twin in process and renders; the only use case it names is the browser-only
        // external-provider step, which has no twin.
        var viewControllers = UiAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Any(m => m.GetCustomAttributes(inherit: false).Any(a => a.GetType().Name == "LegacyPageAttribute")))
            .ToList();

        Assert.NotEmpty(viewControllers);

        var offenders = new List<string>();
        foreach (var type in viewControllers)
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var held = type.GetFields(all).Select(f => f.FieldType)
                .Concat(type.GetProperties(all).Select(p => p.PropertyType))
                .Concat(type.GetConstructors(all).SelectMany(c => c.GetParameters()).Select(p => p.ParameterType));

            offenders.AddRange(held
                .Where(h => ApplicationAssemblies.Contains(h.Assembly) && h.Name != "ExternalSignInUseCase" && !h.IsEnum && !h.Name.EndsWith("Form", StringComparison.Ordinal))
                .Where(h => h.Name.EndsWith("UseCase", StringComparison.Ordinal) || h.Name.EndsWith("Service", StringComparison.Ordinal))
                .Select(h => type.Name + " holds " + h.Name));
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }
}
