# Extension.BaGet.Database.SqlServer

The SQL Server database provider of [BaGet Extension](https://pieroviano.github.io/BaGet/), built on `Microsoft.EntityFrameworkCore.SqlServer`.

```powershell
dotnet add package Extension.BaGet.Database.SqlServer
```

## Usage

Register it in the `BaGetApplication` callback; it activates when `Database:Type` is `SqlServer`:

```csharp
services.AddBaGetWebApplication(app => app.AddSqlServerDatabase());
```

| Setting | Value |
|---|---|
| `Database:Type` | `SqlServer` |
| `Database:ConnectionString` | e.g. `Server=.;Database=BaGet;Trusted_Connection=True;TrustServerCertificate=True` |

## Migrations (contributors)

The project has its own design-time context factory, so `dotnet ef` needs no startup project and no
running database. The tool version is pinned by `.config/dotnet-tools.json`; run `dotnet tool restore` once.

From this folder:

```
dotnet ef migrations add MigrationName --context SqlServerContext --output-dir Migrations
dotnet ef migrations has-pending-model-changes --context SqlServerContext
```

A model change needs a migration in **all four** `BaGet.Database.*` projects, and each must then report
no pending model changes. Keep schema changes additive (new tables, nullable foreign keys): SQLite cannot
alter a foreign key in place.

BaGet applies pending migrations itself when it starts.

- List migrations without a server: `dotnet ef migrations list --context SqlServerContext`.
- Never run `dotnet ef migrations remove --no-build`: with a stale assembly it deleted the previous migration. Delete the generated files by hand and restore the model snapshot from `HEAD`.
- Entity Framework is pinned to 9.0.21, not 10 (`Pomelo.EntityFrameworkCore.MySql` has no EF 10 release); all four providers move together.
