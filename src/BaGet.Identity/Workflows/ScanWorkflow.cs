using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Workflows;

/// <summary>
/// <em>Scan now</em> for one version: checks the artifact (and its dependencies, or the image's packages) against the vulnerability
/// source again and shows what was found. Linked from the findings section of every format's package page. Gated like the other
/// package operations: signed in, the Manage action on the package (<see cref="ScanAccess"/>); refused with 503 while the server is
/// read-only (the result is stored). The scan of a container image can take minutes, so it is queued for the background worker and
/// the screen says so; the other formats are scanned before the answer. Works in the request's repository. Served by
/// <c>ScanArtifactController</c>; the obsolete <c>ScanModel</c> runs the same code.
/// </summary>
internal sealed class ScanWorkflow : PackageWorkflowBase
{
    private readonly IUrlHelper _url;

    /// <summary>Creates the workflow of one request.</summary>
    public ScanWorkflow(HttpContext httpContext, IUrlHelper url, string? name, string? version, string? returnUrl)
        : base(httpContext)
    {
        _url = url;
        Name = name;
        Version = version;
        ReturnUrl = returnUrl;
    }

    /// <summary>The artifact name (NuGet id, npm name, Maven <c>group:artifact</c>, Docker image).</summary>
    public string? Name { get; set; }

    /// <summary>The version (Docker: the manifest digest).</summary>
    public string? Version { get; set; }

    /// <summary>Where the link back goes: the package page the user came from (a local address only).</summary>
    public string? ReturnUrl { get; set; }

    /// <summary>What the screen shows.</summary>
    public ScanViewModel Model { get; } = new();

    /// <summary>The address the link back leads to (the return address when it is local, else the home page).</summary>
    public string BackUrl => !string.IsNullOrEmpty(ReturnUrl) && _url.IsLocalUrl(ReturnUrl) ? ReturnUrl : _url.Content("~/") ?? "/";

    /// <summary>Shows the form (and the stored result) of the version.</summary>
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        if (!await CanScanAsync(cancellationToken)) return StatusCode(403);

        var scans = HttpContext.RequestServices.GetRequiredService<IScanService>();
        Model.Engine = scans.GetEngineStatus();
        Model.Result = await scans.FindAsync(Name!, Version!, cancellationToken);

        return Present();
    }

    /// <summary>Scans the version now (or queues it, for an image) and shows the outcome.</summary>
    public async Task<IActionResult> PostAsync(CancellationToken cancellationToken)
    {
        if (!await CanScanAsync(cancellationToken)) return StatusCode(403);

        var services = HttpContext.RequestServices;
        var scans = services.GetRequiredService<IScanService>();
        Model.Engine = scans.GetEngineStatus();
        if (!Model.Engine.Available)
        {
            return Refuse(409, Model.Engine.Reason ?? "Scanning is not available.");
        }

        var repository = services.GetRequiredService<IRepositoryContext>().Current;
        var user = (await UserManager.GetUserAsync(User))?.UserName;
        services.GetRequiredService<IAdminAudit>().Record(user, "scan.run", $"repository {repository.Name}, {Name} {Version}");

        try
        {
            var scanner = services.GetFormatService<IFormatScanner>(repository.Format);
            if (scanner?.Engine == ScanEngine.Image)
            {
                if (!await scans.IsStoredAsync(Name!, Version!, cancellationToken)) return Refuse(404, "That version does not exist in this repository.");

                services.GetRequiredService<ScanQueue>().Enqueue(repository, Name!, Version!);
                Model.Result = await scans.FindAsync(Name!, Version!, cancellationToken);
                Model.Message = "The image is queued for a scan with Trivy, which can take a few minutes. Reload this page to see the result.";

                return Present();
            }

            Model.Result = await scans.ScanAsync(Name!, Version!, cancellationToken);
        }
        catch (ScanUnavailableException e)
        {
            return Refuse(409, e.Message);
        }

        if (Model.Result == null) return Refuse(404, "That version does not exist in this repository.");

        Model.Result = await scans.FindAsync(Name!, Version!, cancellationToken);
        Model.Message = Model.Result?.Error != null && Model.Result.ScannedAt == null
            ? "The scan failed: " + Model.Result.Error
            : Model.Result?.FindingCount > 0
                ? $"Scanned: {Model.Result.FindingCount} finding(s), the worst is {ScanSeverities.Name(Model.Result.Highest)}."
                : "Scanned: no known vulnerabilities.";
        Model.Failed = Model.Result?.Error != null && Model.Result.ScannedAt == null;

        return Present();
    }

    private async Task<bool> CanScanAsync(CancellationToken cancellationToken)
        => !string.IsNullOrEmpty(Name)
           && !string.IsNullOrEmpty(Version)
           && await ScanAccess.CanScanAsync(HttpContext.RequestServices, User, Name, cancellationToken);

    private IActionResult Refuse(int status, string message)
    {
        Model.Message = message;
        Model.Failed = true;
        HttpContext.Response.StatusCode = status;

        return Present();
    }

    private IActionResult Present()
    {
        Model.Name = Name;
        Model.Version = Version;
        Model.ReturnUrl = ReturnUrl;
        Model.BackUrl = BackUrl;

        return Show;
    }
}
