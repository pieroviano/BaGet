using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NuGet.Versioning;

namespace BaGet.Web;

/// <summary>
/// Records a report against a package version, for its owners and the administrator.
/// The page works for any ID and version: a client expands the report abuse template
/// from whatever it is looking at.
/// </summary>
public class ReportAbuseModel : PageModel
{
    private readonly IContext _context;
    private readonly SystemTime _time;

    public ReportAbuseModel(IContext context, SystemTime time)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    [BindProperty(SupportsGet = true)]
    public string? Id { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Version { get; set; }

    [BindProperty]
    [Required]
    public string? Reason { get; set; }

    [BindProperty]
    [Required]
    [StringLength(AbstractContext<DbContext>.DefaultMaxStringLength)]
    public string? Details { get; set; }

    [BindProperty]
    [EmailAddress]
    [StringLength(AbstractContext<DbContext>.MaxEmailLength)]
    [Display(Name = "Your email (optional)")]
    public string? ReporterEmail { get; set; }

    public bool Submitted { get; private set; }

    /// <summary>
    /// Reports are kept in the database, which Azure Table storage cannot do.
    /// </summary>
    public bool CanReport => !(_context is NullContext);

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!CanReport) return NotFound();

        if (!AbuseReport.Reasons.Contains(Reason, StringComparer.Ordinal))
        {
            ModelState.AddModelError(nameof(Reason), "Choose one of the reasons listed.");
        }

        if (Id == null || Id.Length > AbstractContext<DbContext>.MaxPackageIdLength
                       || !NuGetVersion.TryParse(Version, out var version))
        {
            return NotFound();
        }

        if (!ModelState.IsValid) return Page();

        #pragma warning disable CS8602
        _context.AbuseReports.Add(new AbuseReport
        {
            PackageId = Id,
            PackageVersion = version.ToNormalizedString(),
            Reason = Reason,
            Details = Details,
            ReporterEmail = string.IsNullOrWhiteSpace(ReporterEmail) ? null : ReporterEmail.Trim(),
            Created = _time.UtcNow,
        });
        #pragma warning restore CS8602
        await _context.SaveChangesAsync(cancellationToken);

        Submitted = true;

        return Page();
    }
}