using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account
{
    /// <summary>
    /// Signing out is a POST, so a link elsewhere cannot sign the user out.
    /// </summary>
    public class LogoutModel : AccountPageModel
    {
        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await SignInManager.SignOutAsync();

            return RedirectToLocal(null);
        }
    }
}
