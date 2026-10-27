using System.Collections.Generic;

namespace BaGet.Web.V3;

/// <summary>What <see cref="UiActionMap"/> knows about the controller actions at one version of the action descriptors.</summary>
internal sealed record UiActionMapSnapshot(
    int Version,
    HashSet<string> RazorPages,
    Dictionary<string, (string Controller, string Action)> LegacyPages,
    HashSet<string> RepositoryLegacyPages,
    HashSet<string> RepositoryActions);
