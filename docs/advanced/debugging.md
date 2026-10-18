# Debugging BaGet Extension

## Run

In a fresh clone create the `Packages` folder at the repository root first: nuget.config lists the untracked `Packages` folder as a package source, and restore fails with NU1301 without it.

```
cd src/BaGet
dotnet run -p:GeneratePackageOnBuild=false
```

`src/Directory.Build.props` packs on every build into the shared `Packages` folder; the flag stops that.
`dotnet run` may rewrite `src/BaGet/Properties/launchSettings.json` (it drops the IIS Express profile):
revert it rather than committing it.

## Build and test

```
dotnet build BaGet.sln -p:GeneratePackageOnBuild=false
dotnet test BaGet.sln -p:GeneratePackageOnBuild=false
dotnet test tests/BaGet.Tests/BaGet.Tests.csproj -p:GeneratePackageOnBuild=false --filter "FullyQualifiedName~UploadPageTests"
```

Tests need no external services. `tests/BaGet.Tests/Support/BaGetApplication.cs` runs the real pipeline
over HTTP with SQLite in a temporary directory; it supplies settings with `builder.UseSetting(...)`.

## Port

BaGet Extension listens on `http://localhost:6060`. `Kestrel:Endpoints:Http:Url` in `appsettings.json` outranks
`--urls` and `ASPNETCORE_URLS`, so those have no effect. Override it with the environment variable
`Kestrel__Endpoints__Http__Url`.

## Logging

The `Logging` section of `appsettings.json` sets levels; the shipped `Default` is `Warning` for the
console and debug providers. Raise it for a namespace, for example:

```json
{ "Logging": { "Console": { "LogLevel": { "BaGet": "Debug" } } } }
```

## Command line

| Command | Does |
| --- | --- |
| `dotnet BaGet.dll import downloads` | imports nuget.org download counts |
| `dotnet BaGet.dll catalog rebuild` | appends the catalog entries missing for existing packages |
| `dotnet BaGet.dll account reset-password <user> [--email <address>]` | sets a generated password, switches off two-factor, lifts lockout; needs `Accounts:Enabled` |
