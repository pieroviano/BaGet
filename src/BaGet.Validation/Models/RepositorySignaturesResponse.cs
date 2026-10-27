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
using BaGet.Validation.Signing;

namespace BaGet.Validation.Models;

public class RepositorySignaturesResponse
{
    [JsonPropertyName("allRepositorySigned")]
    public bool AllRepositorySigned { get; set; }

    [JsonPropertyName("signingCertificates")]
    public IReadOnlyList<RepositorySigningCertificate>? SigningCertificates { get; set; }
}
