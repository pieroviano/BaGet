## Contribute

Please read the [code of conduct] before contributing.

If you want to run from BaGet's source code:

1. Install the [.NET SDK] (BaGet targets .NET 10)
2. Run `git clone https://github.com/loic-sharma/BaGet.git`
3. Navigate to `./BaGet/src/BaGet`
4. Start the service with `dotnet run`
5. Open the URL `http://localhost:6060/v3/index.json` in your browser

Run the tests with `dotnet test BaGet.sln` from the repository root. They need no external services:
the integration tests use SQLite in a temporary directory.

A database change needs a migration for each of the four `src/BaGet.Database.*` providers; see the
readme in any of them.

[Code of conduct]: CODE_OF_CONDUCT.md
[.NET SDK]: https://www.microsoft.com/net/download
