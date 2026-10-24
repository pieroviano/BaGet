using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using BaGet.Ha;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BaGet.Tests;

/// <summary>
/// With <c>Ha:Enabled</c> the key ring lives in the database next to the secrets it protects, so <c>Ha:KeyCertificatePath</c> must
/// encrypt the stored keys (without it they are plain XML, which the node warns about at start).
/// </summary>
public class HaKeyProtectionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "baget-ha-" + Guid.NewGuid().ToString("N"));

    public HaKeyProtectionTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string WriteCertificate(string password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=baget-keys", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var path = Path.Combine(_folder, "keys.pfx");
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));

        return path;
    }

    private static List<string> StoredKeysFor(Dictionary<string, string?> settings)
    {
        settings["Ha:Enabled"] = "true";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var repository = new MemoryXmlRepository();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBaGetHa(configuration);
        services.PostConfigure<KeyManagementOptions>(o => o.XmlRepository = repository);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

        return repository.Elements.Select(e => e.ToString()).ToList();
    }

    [Fact]
    public void WithoutACertificateTheStoredKeysAreReadableXml()
    {
        var stored = StoredKeysFor(new Dictionary<string, string?>());

        Assert.Contains(stored, x => x.Contains("masterKey", StringComparison.Ordinal) && !x.Contains("EncryptedData", StringComparison.Ordinal));
    }

    [Fact]
    public void WithACertificateTheStoredKeysAreEncryptedAndStillUsable()
    {
        var path = WriteCertificate("secret");
        var stored = StoredKeysFor(new Dictionary<string, string?> { { "Ha:KeyCertificatePath", path }, { "Ha:KeyCertificatePassword", "secret" } });

        Assert.Contains(stored, x => x.Contains("EncryptedData", StringComparison.Ordinal));
        Assert.DoesNotContain(stored, x => x.Contains("<value>", StringComparison.Ordinal) && x.Contains("masterKey", StringComparison.Ordinal) && !x.Contains("EncryptedData", StringComparison.Ordinal));
    }

    [Fact]
    public void ACertificateThatCannotBeLoadedStopsTheRegistrationWithTheReason()
    {
        var e = Assert.Throws<InvalidOperationException>(() => StoredKeysFor(new Dictionary<string, string?> { { "Ha:KeyCertificatePath", Path.Combine(_folder, "missing.pfx") } }));

        Assert.Contains("Ha:KeyCertificatePath", e.Message);
    }

    private sealed class MemoryXmlRepository : IXmlRepository
    {
        public List<XElement> Elements { get; } = new();

        public IReadOnlyCollection<XElement> GetAllElements() => Elements.ToList();

        public void StoreElement(XElement element, string friendlyName) => Elements.Add(element);
    }
}
