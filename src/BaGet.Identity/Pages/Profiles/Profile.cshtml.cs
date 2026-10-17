using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Pages.Account;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Profiles
{
    /// <summary>
    /// A user's or an organization's public page: the packages it owns.
    /// </summary>
    public class ProfileModel : AccountPageModel
    {
        [BindProperty(SupportsGet = true)]
        public string Name { get; set; }

        public bool IsOrganization { get; private set; }

        public IReadOnlyList<PackageDownloads> Packages { get; private set; } = Array.Empty<PackageDownloads>();

        public IReadOnlyList<string> Members { get; private set; } = Array.Empty<string>();

        public bool IsCurrentUser { get; private set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            var context = HttpContext.RequestServices.GetRequiredService<IContext>();
            var user = await UserManager.FindByNameAsync(Name);

            IReadOnlyList<string> ids;
            if (user != null)
            {
                Name = user.UserName;
                IsCurrentUser = User.Identity?.IsAuthenticated == true && UserManager.GetUserId(User) == user.Id;

                // Only the packages owned in the user's own name: an organization's are on its page.
                var lowerName = user.UserName.ToLowerInvariant();
                ids = await context.PackageOwners
                    .AsNoTracking()
                    .Where(o => o.OwnerName.ToLower() == lowerName)
                    .Select(o => o.PackageId)
                    .ToListAsync(cancellationToken);
            }
            else
            {
                var members = await Ownership.GetMembersAsync(Name, cancellationToken);
                var lowerName = Name.ToLowerInvariant();
                var organization = await context.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Name.ToLower() == lowerName, cancellationToken);
                if (organization == null) return NotFound();

                Name = organization.Name;
                IsOrganization = true;

                var memberIds = members.Select(m => m.UserId).ToList();
                Members = await context.Users
                    .AsNoTracking()
                    .Where(u => memberIds.Contains(u.Id))
                    .Select(u => u.UserName)
                    .OrderBy(n => n)
                    .ToListAsync(cancellationToken);

                ids = await context.PackageOwners
                    .AsNoTracking()
                    .Where(o => o.OwnerName.ToLower() == lowerName)
                    .Select(o => o.PackageId)
                    .ToListAsync(cancellationToken);
            }

            var lowerIds = ids.Select(i => i.ToLowerInvariant()).Distinct().ToList();
            Packages = await context.Packages
                .AsNoTracking()
                .Where(p => lowerIds.Contains(p.Id.ToLower()))
                .GroupBy(p => p.Id)
                .Select(g => new PackageDownloads { Id = g.Key, Downloads = g.Sum(p => p.Downloads) })
                .OrderBy(p => p.Id)
                .ToListAsync(cancellationToken);

            return Page();
        }
    }
}
