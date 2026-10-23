using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Pages.Account;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Packages;

/// <summary>
/// <em>Scan now</em> for one version: checks the artifact (and its dependencies, or the image's packages) against the vulnerability
/// source again and shows what was found. Linked from the findings section of every format's package page. Gated like the other
/// package operations: signed in, the Manage action on the package (<see cref="ScanAccess"/>); refused with 503 while the server is
/// read-only (the result is stored). The scan of a container image can take minutes, so it is queued for the background worker and
/// the page says so; the other formats are scanned before the answer. Works in the request's repository.
/// </summary>
public class ScanModel : AccountPageModel
{
    protected override bool RequiresSignIn => true;

    /// <summary>The artifact name (NuGet id, npm name, Maven <c>group:artifact</c>, Docker image).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get; set; }

    /// <summary>The version (Docker: the manifest digest).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Version { get; set; }

    /// <summary>Where the link back goes: the package page the user came from (a local address only).</summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>What happened, for the page to show.</summary>
    public string? Message { get; private set; }

    /// <summary>Whether <see cref="Message"/> reports a refusal.</summary>
    public bool Failed { get; private set; }

    /// <summary>The result just stored, with its findings, or null.</summary>
    public ScanResult? Result { get; private set; }

    /// <summary>The scanner status of the repository, for the explanation when it cannot run.</summary>
    public ScanEngineStatus? Engine { get; private set; }

    /// <summary>The address the link back leads to (the return address when it is local, else the home page).</summary>
    public string BackUrl => !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : Url.Content("~/");

    /// <summary>Shows the form (and the stored result) of the version.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await CanScanAsync(cancellationToken)) return StatusCode(403);

        var scans = HttpContext.RequestServices.GetRequiredService<IScanService>();
        Engine = scans.GetEngineStatus();
        Result = await scans.FindAsync(Name!, Version!, cancellationToken);

        return Page();
    }

    /// <summary>Scans the version now (or queues it, for an image) and shows the outcome.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await CanScanAsync(cancellationToken)) return StatusCode(403);

        var services = HttpContext.RequestServices;
        var scans = services.GetRequiredService<IScanService>();
        Engine = scans.GetEngineStatus();
        if (!Engine.Available)
        {
            return Refuse(409, Engine.Reason ?? "Scanning is not available.");
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
                Result = await scans.FindAsync(Name!, Version!, cancellationToken);
                Message = "The image is queued for a scan with Trivy, which can take a few minutes. Reload this page to see the result.";

                return Page();
            }

            Result = await scans.ScanAsync(Name!, Version!, cancellationToken);
        }
        catch (ScanUnavailableException e)
        {
            return Refuse(409, e.Message);
        }

        if (Result == null) return Refuse(404, "That version does not exist in this repository.");

        Result = await scans.FindAsync(Name!, Version!, cancellationToken);
        Message = Result?.Error != null && Result.ScannedAt == null
            ? "The scan failed: " + Result.Error
            : Result?.FindingCount > 0
                ? $"Scanned: {Result.FindingCount} finding(s), the worst is {ScanSeverities.Name(Result.Highest)}."
                : "Scanned: no known vulnerabilities.";
        Failed = Result?.Error != null && Result.ScannedAt == null;

        return Page();
    }

    private async Task<bool> CanScanAsync(CancellationToken cancellationToken)
        => !string.IsNullOrEmpty(Name)
           && !string.IsNullOrEmpty(Version)
           && await ScanAccess.CanScanAsync(HttpContext.RequestServices, User, Name, cancellationToken);

    private IActionResult Refuse(int status, string message)
    {
        Message = message;
        Failed = true;
        Response.StatusCode = status;

        return Page();
    }
}
