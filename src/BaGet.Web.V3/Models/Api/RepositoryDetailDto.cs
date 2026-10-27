using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web.V3.Models.Api;

/// <summary>A repository with what its kind adds.</summary>
public sealed record RepositoryDetailDto(
    RepositoryDto Repository,
    string? Project,
    RemoteSettingsDto? Remote,
    RemoteCacheOverview? Cache,
    VirtualDto? Virtual,
    IReadOnlyList<string> ReferencedBy);
