using System.Collections.Generic;
using BaGet.Core;

namespace BaGet.Identity.Pages.Admin;

/// <summary>A package ID as the administrator pages list it.</summary>
/// <param name="Package">The ID summarised across its versions.</param>
/// <param name="Owners">The names of its owners; empty for an ownerless ID.</param>
/// <param name="RepositoryName">The repository the ID is in; ownership and versions are per repository.</param>
public record AdminPackageRow(
    Account.PackagesModel.OwnedPackage Package,
    IReadOnlyList<string> Owners,
    string RepositoryName = Repository.DefaultName);
