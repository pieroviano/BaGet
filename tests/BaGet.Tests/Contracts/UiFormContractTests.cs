using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BaGet.Tests.Contracts;

/// <summary>
/// Contract 2 of the Razor Pages to MVC migration: what every page answers to an anonymous visitor, a signed-in user and an
/// administrator on a seeded server (status, redirect target, title, every form with its handler, fields and antiforgery
/// token, every link to a BaGet page), in <c>Contracts/ui-forms.txt</c>. It must stay byte-identical after every migration
/// track. A second test posts every form without an antiforgery token and expects 400.
/// </summary>
public class UiFormContractTests : IDisposable
{
    private readonly UiContractHarness _h;

    public UiFormContractTests(ITestOutputHelper output)
    {
        _h = new UiContractHarness(output);
    }

    public void Dispose() => _h.Dispose();

    /// <summary>
    /// The addresses requested for every page (by page name): the page at its own route and, for the repository pages, in a
    /// repository of the matching format and of another one (the format and virtual rules answer 404 or redirect).
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string[]> Urls = new Dictionary<string, string[]>
    {
        ["/Account/Access"] = new[] { "/account/access" },
        ["/Account/ApiKeys"] = new[] { "/account/apikeys" },
        ["/Account/ConfirmEmail"] = new[] { "/account/confirmemail", "/account/confirmemail?userId=nobody&code=x" },
        ["/Account/ForgotPassword"] = new[] { "/account/forgotpassword" },
        ["/Account/Login"] = new[] { "/account/login", "/account/login?returnUrl=%2Fstats", "/account/login?handler=Callback" },
        ["/Account/LoginWith2fa"] = new[] { "/account/loginwith2fa" },
        ["/Account/LoginWithRecoveryCode"] = new[] { "/account/loginwithrecoverycode" },
        ["/Account/Logout"] = new[] { "/account/logout" },
        ["/Account/Organizations"] = new[] { "/account/organizations" },
        ["/Account/Packages"] = new[] { "/account/packages" },
        ["/Account/Profile"] = new[] { "/account/profile" },
        ["/Account/Reauthenticate"] = new[] { "/account/reauthenticate", "/account/reauthenticate?returnUrl=%2Faccount%2Fsettings" },
        ["/Account/Register"] = new[] { "/account/register" },
        ["/Account/ResetPassword"] = new[] { "/account/resetpassword", "/account/resetpassword?code=x&userId=alice" },
        ["/Account/Settings"] = new[] { "/account/settings" },
        ["/Account/TwoFactor"] = new[] { "/account/twofactor" },
        ["/Admin/Abuse"] = new[] { "/admin/abuse" },
        ["/Admin/AccessUnavailable"] = new[] { "/admin/access-unavailable" },
        ["/Admin/Api"] = new[] { "/admin/api" },
        ["/Admin/Audit"] = new[] { "/admin/audit", "/admin/audit?actor=contract" },
        ["/Admin/Cluster"] = new[] { "/admin/cluster" },
        ["/Admin/DeleteUser"] = new[] { "/admin/users/alice/delete", "/admin/users/nobody/delete" },
        ["/Admin/Formats"] = new[] { "/admin/formats" },
        ["/Admin/GroupDetail"] = new[] { "/admin/groups/team", "/admin/groups/nobody" },
        ["/Admin/Groups"] = new[] { "/admin/groups" },
        ["/Admin/Operations"] = new[] { "/admin/operations" },
        ["/Admin/OrganizationDetail"] = new[] { "/admin/organizations/acme", "/admin/organizations/nobody" },
        ["/Admin/Organizations"] = new[] { "/admin/organizations" },
        ["/Admin/Packages"] = new[] { "/admin/packages", "/r/second/admin/packages" },
        ["/Admin/PermissionDetail"] = new[] { "/admin/permissions/t1", "/admin/permissions/nobody" },
        ["/Admin/Permissions"] = new[] { "/admin/permissions" },
        ["/Admin/ProjectDetail"] = new[] { "/admin/projects/shop", "/admin/projects/nobody" },
        ["/Admin/Projects"] = new[] { "/admin/projects" },
        ["/Admin/Replication"] = new[] { "/admin/replication" },
        ["/Admin/ReplicationConfig"] = new[] { "/admin/replication/1", "/admin/replication/99" },
        ["/Admin/Repositories"] = new[] { "/admin/repositories" },
        ["/Admin/RepositoryDelete"] = new[] { "/admin/repositories/second/delete", "/admin/repositories/default/delete" },
        ["/Admin/RepositoryEdit"] = new[] { "/admin/repositories/second", "/admin/repositories/web", "/admin/repositories/libs", "/admin/repositories/reg", "/admin/repositories/up", "/admin/repositories/virt", "/admin/repositories/default" },
        ["/Admin/Retention"] = new[] { "/admin/retention" },
        ["/Admin/RetentionHolds"] = new[] { "/admin/retention/holds" },
        ["/Admin/RetentionPolicy"] = new[] { "/admin/retention/1", "/admin/retention/99" },
        ["/Admin/Scanning"] = new[] { "/admin/scanning" },
        ["/Admin/ScanProject"] = new[] { "/admin/scanning/projects/shop" },
        ["/Admin/ScanRepository"] = new[] { "/admin/scanning/second", "/admin/scanning/default" },
        ["/Admin/Settings"] = new[] { "/admin/settings", "/admin/settings?handler=CopyStatus" },
        ["/Admin/UserDetail"] = new[] { "/admin/users/alice", "/admin/users/nobody" },
        ["/Admin/UserPackages"] = new[] { "/admin/users/alice/packages", "/r/second/admin/users/alice/packages" },
        ["/Admin/Users"] = new[] { "/admin/users", "/admin/users?q=ali" },
        ["/Admin/Validation"] = new[] { "/admin/validation" },
        ["/DockerImage"] = new[] { "/r/reg/docker-images/hello", "/r/reg/docker-images/nothing", "/r/web/docker-images/hello" },
        ["/DockerImages"] = new[] { "/docker-images", "/r/reg/docker-images", "/r/second/docker-images" },
        ["/Index"] = new[] { "/", "/packages", "/?q=contoso", "/r/second/", "/r/second/packages", "/r/web/", "/r/libs/", "/r/reg/", "/r/virt/", "/r/up/", "/r/nothing/" },
        ["/MavenArtifact"] = new[] { "/r/libs/maven-artifacts/org.acme/lib", "/r/libs/maven-artifacts/org.acme/lib/1.0", "/r/libs/maven-artifacts/org.acme/none" },
        ["/MavenArtifacts"] = new[] { "/maven-artifacts", "/r/libs/maven-artifacts", "/r/second/maven-artifacts" },
        ["/MavenUpload"] = new[] { "/maven-upload", "/r/libs/maven-upload", "/r/second/maven-upload" },
        ["/NpmPackage"] = new[] { "/r/web/npm-packages/left-pad", "/r/web/npm-packages/none", "/r/second/npm-packages/left-pad" },
        ["/NpmPackages"] = new[] { "/npm-packages", "/r/web/npm-packages", "/r/second/npm-packages" },
        ["/NpmUpload"] = new[] { "/npm-upload", "/r/web/npm-upload", "/r/second/npm-upload" },
        ["/Package"] = new[] { "/packages/Contoso.Lib", "/packages/Contoso.Lib/1.0.0", "/packages/Contoso.Lib/9.9.9", "/packages/Nobody.Lib", "/r/second/packages/Second.Lib", "/r/second/packages/Contoso.Lib", "/r/virt/packages/Contoso.Lib" },
        ["/Packages/ContactOwners"] = new[] { "/packages/Contoso.Lib/contact-owners", "/r/second/packages/Second.Lib/contact-owners", "/r/virt/packages/Contoso.Lib/contact-owners" },
        ["/Packages/DockerManage"] = new[] { "/r/reg/docker-manage/hello", "/r/reg/docker-manage/nothing", "/r/web/docker-manage/hello", "/docker-manage/hello" },
        ["/Packages/Manage"] = new[] { "/packages/Contoso.Lib/manage", "/packages/Nobody.Lib/manage", "/r/second/packages/Second.Lib/manage", "/r/virt/packages/Contoso.Lib/manage" },
        ["/Packages/MavenManage"] = new[] { "/r/libs/maven-manage/org.acme/lib", "/r/libs/maven-manage/org.acme/none", "/r/second/maven-manage/org.acme/lib", "/maven-manage/org.acme/lib" },
        ["/Packages/NpmManage"] = new[] { "/r/web/npm-manage/left-pad", "/r/web/npm-manage/none", "/r/second/npm-manage/left-pad", "/npm-manage/left-pad" },
        ["/Packages/Scan"] = new[] { "/scan-artifact", "/scan-artifact?name=Contoso.Lib&version=1.0.0", "/r/second/scan-artifact", "/r/web/scan-artifact", "/r/virt/scan-artifact" },
        ["/Packages/ValidationStatus"] = new[] { "/packages/Contoso.Lib/validation", "/packages/Nobody.Lib/validation" },
        ["/Privacy"] = new[] { "/privacy" },
        ["/Profiles/Profile"] = new[] { "/profiles/alice", "/profiles/nobody", "/r/second/profiles/alice" },
        ["/ReportAbuse"] = new[] { "/packages/Contoso.Lib/1.0.0/report-abuse", "/packages/Contoso.Lib/9.9.9/report-abuse", "/r/second/packages/Second.Lib/1.0.0/report-abuse", "/r/virt/packages/Contoso.Lib/1.0.0/report-abuse" },
        ["/Statistics"] = new[] { "/stats", "/r/second/stats", "/r/virt/stats", "/r/web/stats" },
        ["/Terms"] = new[] { "/terms" },
        ["/Upload"] = new[] { "/upload", "/r/second/upload", "/r/virt/upload", "/r/web/upload", "/r/up/upload" },
    };

    private IEnumerable<(string Name, Browser Browser)> Callers
    {
        get
        {
            yield return ("anonymous", _h.Anonymous);
            yield return ("user", _h.Alice);
            yield return ("admin", _h.Root);
        }
    }

    [Fact]
    public void EveryPageHasAtLeastOneAddress()
    {
        var pages = UiRoutes.Read(_h.App.Services.GetRequiredService<EndpointDataSource>()).Select(e => e.Page).Distinct().ToList();

        Assert.Empty(pages.Except(Urls.Keys));
        Assert.Empty(Urls.Keys.Except(pages));
    }

    [Fact]
    public async Task TheRenderedPagesMatchTheSnapshot()
    {
        var text = new StringBuilder();
        foreach (var (caller, browser) in Callers)
        {
            foreach (var url in Urls.Values.SelectMany(u => u).OrderBy(u => u, StringComparer.Ordinal))
            {
                text.Append("== ").Append(caller).Append(" GET ").AppendLine(url);

                using var response = await browser.GetAsync(url);
                text.Append("status=").Append((int)response.StatusCode);
                if (response.Headers.Location != null)
                {
                    text.Append(" location=").Append(UiHtml.Normalize(response.Headers.Location.ToString()));
                }

                text.AppendLine();
                if (response.StatusCode != HttpStatusCode.OK) continue;

                var summary = UiHtml.Summarize(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                text.Append("title=").AppendLine(summary.Title);
                foreach (var form in summary.Forms)
                {
                    text.Append("form ").Append(form.Method).Append(' ').Append(form.Action.Length == 0 ? "(self)" : form.Action)
                        .Append(" antiforgery=").Append(form.Antiforgery ? "yes" : "no")
                        .Append(" fields=[").Append(string.Join(",", form.Fields)).AppendLine("]");
                }

                foreach (var link in summary.Links) text.Append("link ").AppendLine(link);
            }
        }

        ContractSnapshot.Verify("ui-forms.txt", text.ToString());
    }

    [Fact]
    public async Task EveryPostWithoutAnAntiforgeryTokenIsRefused()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var targets = new List<(string Caller, Browser Browser, string Url)>();
        foreach (var (caller, browser) in Callers)
        {
            foreach (var url in Urls.Values.SelectMany(u => u).Distinct())
            {
                using var response = await browser.GetAsync(url);
                if (response.StatusCode != HttpStatusCode.OK) continue;

                foreach (var form in UiHtml.Summarize(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Forms.Where(f => f.Method == "POST"))
                {
                    var action = form.Action.Length == 0 ? url : form.Action;
                    if (seen.Add($"{caller} {action}")) targets.Add((caller, browser, action));
                }
            }
        }

        Assert.True(targets.Count > 50, "expected the forms of the pages");

        var accepted = new List<string>();
        foreach (var (caller, browser, action) in targets)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, action) { Content = new FormUrlEncodedContent(new Dictionary<string, string>()) };
            using var response = await browser.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.BadRequest) accepted.Add($"{caller} POST {action} -> {(int)response.StatusCode}");
        }

        Assert.True(accepted.Count == 0, "Posts without an antiforgery token that were not refused with 400:" + Environment.NewLine + string.Join(Environment.NewLine, accepted));
    }
}
