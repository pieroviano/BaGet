using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace BaGet.Web.V3.Models.Protocols;

public class DeprecationRequest
{
    [JsonPropertyName("reasons")]
    public IReadOnlyList<string>? Reasons { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("alternatePackageId")]
    public string? AlternatePackageId { get; set; }

    [JsonPropertyName("alternateVersionRange")]
    public string? AlternateVersionRange { get; set; }
}
