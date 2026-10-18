# BaGet's PostgreSQL Database Provider

This project contains BaGet's PostgreSQL database provider.

## Migrations

The project has its own design-time context factory, so `dotnet ef` needs no startup project and no
running database. The tool version is pinned by `.config/dotnet-tools.json`; run `dotnet tool restore` once.

From this folder:

```
dotnet ef migrations add MigrationName --context PostgreSqlContext --output-dir Migrations
dotnet ef migrations has-pending-model-changes --context PostgreSqlContext
```

A model change needs a migration in **all four** `BaGet.Database.*` projects, and each must then report
no pending model changes. Keep schema changes additive (new tables, nullable foreign keys): SQLite cannot
alter a foreign key in place.

BaGet applies pending migrations itself when it starts.

- List migrations without a server: `dotnet ef migrations list --context PostgreSqlContext`.
- Never run `dotnet ef migrations remove --no-build`: with a stale assembly it deleted the previous migration. Delete the generated files by hand and restore the model snapshot from `HEAD`.
- Entity Framework is pinned to 9.0.21, not 10 (`Pomelo.EntityFrameworkCore.MySql` has no EF 10 release); all four providers move together.
