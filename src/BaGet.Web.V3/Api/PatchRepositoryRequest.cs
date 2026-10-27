using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web.V3.Api;

/// <summary>A change of a repository's anonymous-read switch.</summary>
public sealed class PatchRepositoryRequest
{
    /// <summary>The new value.</summary>
    public bool? AnonymousRead { get; set; }
}
