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

/// <summary>
/// Advertises RepositorySignatures/5.0.0 while a signing certificate is configured.
/// </summary>
public class RepositorySignaturesServiceIndexProvider : IServiceIndexResourceProvider
{
    private readonly RepositorySigner _signer;
    private readonly IUrlGenerator _url;

    public RepositorySignaturesServiceIndexProvider(RepositorySigner signer, IUrlGenerator url)
    {
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
        _url = url ?? throw new ArgumentNullException(nameof(url));
    }

    public IEnumerable<ServiceIndexItem> GetResources()
    {
        if (!_signer.IsConfigured) yield break;

        yield return new ServiceIndexItem
        {
            ResourceUrl = _url.GetRepositorySignaturesUrl(),
            Type = "RepositorySignatures/5.0.0",
        };
    }
}