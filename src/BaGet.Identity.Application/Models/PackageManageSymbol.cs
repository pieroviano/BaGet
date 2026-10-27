using System;
using System.Collections.Generic;
using System.Linq;
using BaGet.Core;

namespace BaGet.Identity.Application.Models;

/// <summary>An indexed PDB of a version.</summary>
/// <param name="FileName">The PDB file name.</param>
/// <param name="Key">Its signature key.</param>
public sealed record PackageManageSymbol(string FileName, string Key);
