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

public class ValidationStatusResponse
{
    [JsonPropertyName("submissionId")]
    public string? SubmissionId { get; set; }

    /// <summary>
    /// "Validating", "Available" or "Quarantined".
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("steps")]
    public IReadOnlyList<ValidationStep>? Steps { get; set; }
}
