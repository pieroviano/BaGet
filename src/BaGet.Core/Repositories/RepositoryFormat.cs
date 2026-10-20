namespace BaGet.Core;

/// <summary>
/// The package format a <see cref="Repository"/> serves. Stored as its name, so a value may be appended
/// but never renumbered or renamed.
/// </summary>
public enum RepositoryFormat
{
    /// <summary>
    /// NuGet (V3 and V2 feeds, symbols). The only format a repository can have today.
    /// </summary>
    NuGet = 0,
}

/// <summary>
/// How a <see cref="Repository"/> gets its content. Stored as its name.
/// </summary>
public enum RepositoryKind
{
    /// <summary>
    /// Holds what is pushed to it. The only kind a repository can have today.
    /// </summary>
    Local = 0,

    /// <summary>
    /// Caches an upstream repository.
    /// </summary>
    Remote = 1,

    /// <summary>
    /// Presents several repositories as one.
    /// </summary>
    Virtual = 2,
}
