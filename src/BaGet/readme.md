# BaGet

The application's host. It wires the other projects together and serves the [NuGet service APIs](https://docs.microsoft.com/en-us/nuget/api/overview); most of the logic lives in `BaGet.Core`.

Run without arguments to apply database migrations and start the server. Commands:

| Command | Does |
|---|---|
| `import downloads` | Imports nuget.org's download counts for the packages in the database |
| `catalog rebuild` | Appends the catalog entries missing for the packages in the database, for example packages pushed before the catalog existed |
