# Extension.BaGet.Identity.Application

The account and administration **use cases** of [BaGet Extension](https://pieroviano.github.io/BaGet/): business services (accounts, API keys,
ownership, administrators, rate limiters, email senders), the screen result and the screen gate. No MVC or Razor dependency (test-enforced);
the screens (controllers, views) are in [`Extension.BaGet.Identity`](https://www.nuget.org/packages/Extension.BaGet.Identity).

| Type | Role |
|---|---|
| `ScreenResult<T>` | What a use case returns for a screen: `Status` (Ok, Invalid, NotFound, Forbidden, SignIn, Next), `Model`, `Errors`, `Next`. The JSON UI API serialises it as it is |
| `ScreenGate` | Who may use a screen: accounts on, mail, signed in, administrator (or project administrator), access model present, server writable. View controllers and the JSON API both ask it |
| `UiApiJson` | The JSON options of `/api/ui` (camelCase, enums by name, nulls left out) |
| `AccountAuthenticationService`, `ApiKeyService`, `PackageOwnershipService`, ... | The services `AddBaGetAccounts` registers while accounts are enabled; they moved here with their namespace (`BaGet.Identity`), and `Extension.BaGet.Identity` forwards the types |

`AddBaGetAccounts` (in `Extension.BaGet.Identity`) stays the entry point of a host; it calls `AddBaGetIdentityApplication`.
