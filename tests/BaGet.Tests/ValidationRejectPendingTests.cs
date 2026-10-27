using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Validation.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BaGet.Tests;

/// <summary>A submission stuck pending can be rejected by an administrator and the version pushed again.</summary>
public class ValidationRejectPendingTests : IDisposable
{
    private readonly BaGetApplication _app;

    public ValidationRejectPendingTests(ITestOutputHelper output)
    {
        _app = new BaGetApplication(
            output,
            configOverrides: new Dictionary<string, string> { { "Validation:Enabled", "true" }, { "Accounts:Enabled", "false" } },
            configureServices: services => services.AddSingleton<IPackageValidator>(new NeverEndingValidator()));
    }

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task APendingSubmissionCanBeRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _app.CreateClient();

        using (var package = TestPackages.Build("Stuck.Package", "1.0.0"))
        using (var request = TestPackages.PushRequest(package, null!))
        using (var push = await client.SendAsync(request, ct))
        {
            Assert.Equal(HttpStatusCode.Accepted, push.StatusCode);
        }

        using var scope = _app.Services.CreateScope();
        var administration = scope.ServiceProvider.GetRequiredService<IValidationAdministration>();

        var submission = Assert.Single(await administration.ListAsync(ct));
        Assert.Null(await administration.RejectAsync(submission.SubmissionId, "tester", ct));
        Assert.Empty(await administration.ListAsync(ct));
    }

    private sealed class NeverEndingValidator : IPackageValidator
    {
        public string Name => "Never";

        public async Task<PackageValidatorResult> ValidateAsync(PackageValidationContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return PackageValidatorResult.Valid;
        }
    }
}
