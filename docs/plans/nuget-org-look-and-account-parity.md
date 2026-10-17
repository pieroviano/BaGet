# nuget.org look and feel, accounts on by default, account and site parity

Make BaGet look and behave as much like nuget.org as practical: visible sign-in, account settings with
2FA and password recovery, nuget.org's page design, package page tabs, search sort, avatars, contact
owners.

## Blocking — resolve before coding

| # | Item | Why it blocks | Status / how |
|---|---|---|---|
| **B0** | **Accounts on by default breaks keyless pushes.** With accounts enabled and `ApiKey` empty, `AccountAuthenticationService.AuthenticatePrincipalAsync` returns null for an absent key, by design, so ownership cannot be bypassed. A default install today accepts `dotnet nuget push` with no key. | Any CI pushing keyless to a default-configured BaGet gets **401 after upgrade**. | **Decided: proceed** (D1), with the mitigations in P1: startup warning, upload page message, docs note. Re-confirm before release. |
| B1 | Search sort per provider | `SearchRequest` (`src/BaGet.Core/Search/SearchRequest.cs:12-42`) has no sort. `DatabaseSearchService` can order; Azure Search needs sortable index fields; Azure Table cannot sort server-side. | Read `AzureSearchService` index definition (`src/BaGet.Azure/Search/`) and `TableSearchService`. If a provider cannot sort, it ignores `SortBy` and the UI hides the control (no in-memory sort of one page — that is wrong ordering). |
| B2 | Identity schema for 2FA / email confirmation | Needs `AspNetUsers.TwoFactorEnabled`, `EmailConfirmed`, and `AspNetUserTokens`. | `TwoFactorEnabled` present in all four `*ContextModelSnapshot.cs`. Confirm `AspNetUserTokens` likewise. Expected: **no migration**. |
| B3 | Test harness default | `tests/BaGet.Tests/Support/BaGetApplication.cs` does not set `Accounts:Enabled`; the test host reads `src/BaGet/appsettings.json`. Flipping it there changes ~250 tests. | Pin `{ "Accounts:Enabled", "false" }` in the harness's default settings **first**; tests needing accounts already override it. |
| B4 | Visual assets and licensing | nuget.org's gallery is `NuGet/NuGetGallery` (Apache-2.0); the NuGet logo is a Microsoft trademark. | Write BaGet's own CSS that reproduces the design; copy no logo or images; if any CSS is adapted, keep the Apache-2.0 notice. |
| B5 | Email in tests | No real SMTP in CI. | A capturing fake `IEmailSender<BaGetUser>` registered by the harness; assert on captured messages. |

## Decisions

| # | Decision | Consequence |
|---|---|---|
| D1 | Accounts **on by default**, registration allowed | Enabled in the shipped `appsettings.json`; the C# default `AccountsOptions.Enabled = false` (`src/BaGet.Core/Configuration/AccountsOptions.cs:19`) stays, so hosts embedding `AddBaGetAccounts` are unchanged. B0 applies to the BaGet host. |
| D2 | nuget.org design, **BaGet identity** | nuget.org layout, `#004880` header, typography, search hero, tabs, footer; BaGet name and logo. No NuGet logo. |
| D3 | **Optional SMTP** | `Email:Smtp` section. Unconfigured ⇒ every email-dependent feature (forgot password, email confirmation, change email, contact owners) is hidden, not broken; an admin CLI `baget account reset-password` covers recovery. |
| D4 | Scope: package page, account settings + 2FA, search/home/footer, avatars + contact | Six phases below. |

## Phases

Ordered by dependency. Each phase: tests red first, full suite green (baseline **892**), commit.

### P1 — Accounts on, header links (D1)

| File | Change |
|---|---|
| `tests/BaGet.Tests/Support/BaGetApplication.cs` (settings dictionary, ~line 66) | Add `{ "Accounts:Enabled", "false" }` — B3, done before the next row |
| `src/BaGet/appsettings.json:12` | Add `"Accounts": { "Enabled": true, "AllowRegistration": true }` |
| `src/BaGet/Program.cs` `Main` | Log a warning at startup when accounts are on and `ApiKey` is empty: keyless pushes are refused; create a user API key or set `ApiKey` |
| `src/BaGet.Web/Pages/Upload.cshtml` | CLI tabs state that `-k <your API key>` is required when accounts are on, linking to `~/account/apikeys` |
| `src/BaGet.Web/Pages/Shared/_Layout.cshtml:35-49` | Signed out: **Sign in** (and **Register** when `AllowRegistration`). Signed in: avatar dropdown — Account settings, API keys, Manage packages, Organizations, Upload package, Sign out (nuget.org's menu) |
| `docs/` | Upgrade note for B0 |

### P2 — Optional email (D3)

| File | Change |
|---|---|
| `src/BaGet.Core/Configuration/EmailOptions.cs` (new) | `Smtp { Host, Port, UserName, Password, EnableSsl, From }`; `IsConfigured => !string.IsNullOrEmpty(Smtp.Host) && !string.IsNullOrEmpty(Smtp.From)` |
| `src/BaGet.Identity/SmtpEmailSender.cs` (new) | `IEmailSender<BaGetUser>` over **MailKit** (MIT; Microsoft discourages `System.Net.Mail.SmtpClient` for new code). Version lever in `src/Directory.Build.props` |
| `AccountsServiceCollectionExtensions.AddBaGetAccounts` | Register the sender; `SignIn.RequireConfirmedEmail = email.IsConfigured` |
| `src/BaGet/Program.cs` | `account reset-password <user>` command, printing a one-time password; works without email |

### P3 — Account settings and 2FA

New pages in `src/BaGet.Identity/Pages/Account/`, all `AccountPageModel` with `RequiresSignIn` where applicable:

| Page | Notes |
|---|---|
| `Settings` | Change password; change email (confirmation link when email configured); delete account |
| `ForgotPassword`, `ResetPassword`, `ConfirmEmail` | Shown only when `EmailOptions.IsConfigured`; ForgotPassword never reveals whether an account exists |
| `TwoFactor` | Enable authenticator (TOTP via Identity's `AuthenticatorTokenProvider`, already registered by `AddDefaultTokenProviders`); QR code via **QRCoder** (MIT) rendered server-side as SVG; 10 recovery codes; disable; reset |
| `LoginWith2fa`, `LoginWithRecoveryCode` | `Login.cshtml.cs` handles `SignInResult.RequiresTwoFactor` |

Delete account **refuses while the user is the last owner of any package ID**, consistent with the
existing "last owner is kept" rule in `PackageOwnershipService.RemoveOwnerAsync`; the page lists those
IDs. API keys and organization memberships are removed with the user.

### P4 — Look and feel (D2)

| File | Change |
|---|---|
| `src/BaGet.Web/wwwroot/css/site.css` (429 lines) | Rework into nuget.org's design: `#004880` header, white nav, Segoe UI stack, result cards, tabs, sidebar, footer. Office UI Fabric icons stay |
| `_Layout.cshtml:19-79` | nuget.org header: brand left, Packages / Upload / Statistics / Documentation, account area right; search bar under the header except on home |
| `_Layout.cshtml` after `@RenderBody()` (line 82) | Footer: Documentation, GitHub, Terms of Use and Privacy (when configured), "Powered by BaGet" |
| `src/BaGet.Web/Pages/Index.cshtml` (267 lines) | No query ⇒ nuget.org home: hero search, totals (packages, versions, downloads) from `/stats` data. Results: nuget.org cards — icon, id, "by" owners, downloads, last updated, latest version, tags, description, verified and deprecated badges |
| Terms / Privacy | `Site:TermsOfUsePath` / `Site:PrivacyPolicyPath` point at operator Markdown rendered by a page; links hidden when unset. **No placeholder legal text is invented** |

### P5 — Package page like nuget.org

`src/BaGet.Web/Pages/Package.cshtml` (363 lines):

| Area | Change |
|---|---|
| Lines 38-200 (expandable sections) | Horizontal tabs: **README · Frameworks · Dependencies · Used By · Versions · Release Notes**, keeping each section's existing content |
| Frameworks tab (new) | From `Package.TargetFrameworks` (`Package.cs:82`): declared frameworks grouped .NET / .NET Core / .NET Standard / .NET Framework, plus "computed" compatible frameworks from `NuGet.Frameworks` `CompatibilityProvider` against a fixed list of current TFMs, as nuget.org does. No schema change |
| Install snippets (lines 299-346) | Add Script & Interactive (`#r "nuget: Id, Ver"`), File-based apps (`#:package Id@Ver`), Cake (`#addin`/`#tool`), NUKE (`nuke :add-package`), Central Package Management |
| Sidebar (204-290) | nuget.org "About": last updated, project website, source repository, license, download package, download symbols when present, report package; Owners with avatars; **Contact owners** |

### P6 — Search sort, avatars, contact owners

| Item | Change |
|---|---|
| Sort | `SearchRequest.SortBy` (`Relevance`, `TotalDownloads`, `LastUpdated`), web UI only — the V3 search API has no sort parameter and stays protocol-identical. Providers per B1. Control in `Index.cshtml` filters (lines 16-66), hidden when the provider cannot sort |
| Avatars | Gravatar `https://www.gravatar.com/avatar/{md5(lower(email))}?s=…&d=retro`; organizations hash their name. `Site:UseGravatar` (default true) so a private feed can turn off the request to gravatar.com |
| Contact owners | `/packages/{id}/contact-owners`, signed-in only, hidden without email (D3). Mails user owners and the admins of owning organizations, `Reply-To` the sender, optional copy to self. One message per sender per package per 10 minutes |

## Verification

| # | Check | Expected |
|---|---|---|
| 1 | Full suite | 892 existing green after P1's harness pin, then each phase adds tests |
| 2 | Fresh install, `dotnet run` | Sign in / Register visible; register, sign in, avatar menu shows all six entries |
| 3 | Keyless `dotnet nuget push` on a fresh install | 401, startup warning logged (B0 documented behaviour) |
| 4 | Push with a user API key from `/account/apikeys` | 201, user becomes owner |
| 5 | Email unconfigured | Forgot password, change email, contact owners absent; `baget account reset-password` works |
| 6 | Email configured (fake sender in tests) | Confirmation on register, reset link valid once, expired token refused |
| 7 | 2FA | Enable with a TOTP code; sign-in demands a code; recovery code works once; disable |
| 8 | Delete account as last owner | Refused, IDs listed; after transferring ownership, succeeds and keys are gone |
| 9 | Package page | Six tabs; Frameworks shows declared and computed; every install snippet copies the right text |
| 10 | Sort | Database provider orders correctly; on Azure Table the control is hidden |
| 11 | Contact owners | Reaches every user owner and owning-org admin; second message inside 10 minutes refused |
| 12 | `Accounts:Enabled=false` | Whole site still works with no account links or pages (regression guard) |
| 13 | Visual check | Run the app and compare home, search, package and account pages with nuget.org side by side |

## Risks

| Risk | Likelihood | Handling |
|---|---|---|
| B0: keyless CI breaks on upgrade | **High** for default-config users | Startup warning, upload page text, docs; operator can set `Accounts:Enabled=false` |
| Email features half-working when SMTP is misconfigured | Medium | `IsConfigured` gate; send failures logged and shown as "could not send", never as success |
| Gravatar leaks a hash of the email (dictionary-reversible) to a third party | Medium | `Site:UseGravatar` switch, documented |
| Contact owners used for spam | Medium | Sign-in required, rate limit, sender's address in `Reply-To` |
| 2FA lockout | Low | Recovery codes; admin `reset-password` also clears 2FA |
| CSS rework regresses existing pages | Medium | Verification 13 on every page, including account and manage pages |
| Two new dependencies (MailKit, QRCoder) | Low | Both MIT, single version levers |

## Out of scope

- The NuGet logo or nuget.org wording (D2).
- Microsoft account sign-in: generic OpenID Connect already exists (`Accounts:OpenIdConnect:*`).
- Package ID prefix reservation, email notification preferences, package transfer requests.
- Invented Terms of Use or Privacy text.
