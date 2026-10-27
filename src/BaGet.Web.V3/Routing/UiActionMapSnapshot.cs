using System.Collections.Generic;

namespace BaGet.Web.V3.Routing;

/// <summary>What <see cref="UiActionMap"/> knows about the controller actions at one version of the action descriptors.</summary>
internal sealed record UiActionMapSnapshot(int Version, HashSet<string> RepositoryActions);
