using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Account
{
    public class ApiKeysModel : AccountPageModel
    {
        protected override bool RequiresSignIn => true;

        [BindProperty]
        [StringLength(AbstractContext<Microsoft.EntityFrameworkCore.DbContext>.MaxApiKeyDescriptionLength)]
        public string Description { get; set; }

        [BindProperty]
        public List<string> Scopes { get; set; } = new List<string>();

        [BindProperty]
        [Display(Name = "Package glob pattern")]
        public string PackageGlobPattern { get; set; } = "*";

        [BindProperty]
        [Range(1, 3650)]
        [Display(Name = "Expires after (days)")]
        public int? ExpiresInDays { get; set; } = 365;

        public IReadOnlyList<ApiKey> Keys { get; private set; } = Array.Empty<ApiKey>();

        /// <summary>
        /// The key just created. Shown once: only its hash is kept.
        /// </summary>
        public string CreatedKey { get; private set; }

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            await LoadAsync(cancellationToken);
        }

        public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
        {
            if (Scopes.Count == 0)
            {
                ModelState.AddModelError(nameof(Scopes), "Choose at least one scope.");
            }

            if (ModelState.IsValid)
            {
                var now = HttpContext.RequestServices.GetRequiredService<SystemTime>().UtcNow;

                try
                {
                    CreatedKey = await ApiKeys.CreateAsync(
                        UserManager.GetUserId(User),
                        Description,
                        Scopes,
                        PackageGlobPattern,
                        ExpiresInDays.HasValue ? now.AddDays(ExpiresInDays.Value) : (DateTime?)null,
                        cancellationToken);
                }
                catch (ArgumentException e)
                {
                    ModelState.AddModelError(nameof(Scopes), e.Message);
                }
            }

            await LoadAsync(cancellationToken);

            return Page();
        }

        public async Task<IActionResult> OnPostRevokeAsync(int key, CancellationToken cancellationToken)
        {
            await ApiKeys.RevokeAsync(UserManager.GetUserId(User), key, cancellationToken);

            return RedirectToPage();
        }

        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            Keys = await ApiKeys.ListAsync(UserManager.GetUserId(User), cancellationToken);
        }

        public static string Describe(ApiKey key)
            => string.Join(", ", key.Scopes ?? Array.Empty<string>()) + " on " + key.PackageGlobPattern;

        public static IReadOnlyList<string> AllScopes => ApiKeyScopes.All;
    }
}
