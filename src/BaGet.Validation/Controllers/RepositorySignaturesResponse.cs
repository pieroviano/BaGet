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

public class RepositorySignaturesResponse
{
    [JsonPropertyName("allRepositorySigned")]
    public bool AllRepositorySigned { get; set; }

    [JsonPropertyName("signingCertificates")]
    public IReadOnlyList<RepositorySigningCertificate>? SigningCertificates { get; set; }
}
