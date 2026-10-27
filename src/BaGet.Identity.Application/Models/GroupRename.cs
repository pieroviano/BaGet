using System.Text.Json.Serialization;
using BaGet.Core;

namespace BaGet.Identity.Application.Models;

/// <summary>Renaming a group.</summary>
/// <param name="NewName">The new name.</param>
public sealed record GroupRename(string? NewName);
