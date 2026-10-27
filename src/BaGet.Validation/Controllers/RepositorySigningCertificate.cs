using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Protocol.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NuGet.Versioning;

namespace BaGet.Validation.Controllers;

public class RepositorySigningCertificate
{
    [JsonPropertyName("fingerprints")]
    public IReadOnlyDictionary<string, string>? Fingerprints { get; set; }

    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    [JsonPropertyName("issuer")]
    public string? Issuer { get; set; }

    [JsonPropertyName("notBefore")]
    public DateTime NotBefore { get; set; }

    [JsonPropertyName("notAfter")]
    public DateTime NotAfter { get; set; }

    [JsonPropertyName("contentUrl")]
    public string? ContentUrl { get; set; }
}
