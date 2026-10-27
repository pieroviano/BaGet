using System;
using System.Collections.Generic;
using System.Linq;
using BaGet.Core;

namespace BaGet.Identity.Application.Models;

/// <summary>One row of the operations screen: an operation on a repository.</summary>
/// <param name="Operation">The operation.</param>
/// <param name="Repository">The repository it runs on.</param>
/// <param name="Status">The status of its last run, or null.</param>
public sealed record OperationRow(IAdminOperation Operation, RepositoryInfo Repository, AdminJobStatus? Status);
