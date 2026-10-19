# Extension.BaGet.Web.V2

The legacy NuGet V2 (OData/Atom) feed of [BaGet Extension](https://pieroviano.github.io/BaGet/), for clients that
cannot use V3.

```powershell
dotnet add package Extension.BaGet.Web.V2
```

## Usage

`AddBaGetWebApplication` and `BaGetEndpointBuilder` (`Extension.BaGet.Web`) register the controller and map its
routes under `api/v2` (`$metadata`, `Packages()`, `FindPackagesById()`, `Search()`, `package` push/download/delete,
`symbol`).

| Setting | Value |
|---|---|
| `Feeds:EnableV2` | Default `true`; `false` turns the feed off |

Client source: `https://<server>/api/v2`.

## Links

[Documentation](https://pieroviano.github.io/BaGet/) · [Source](https://github.com/pieroviano/BaGet)
