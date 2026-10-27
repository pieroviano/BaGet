using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaGet.Identity.Application.Services;

/// <summary>What happens to one organization when an administrator deletes a member.</summary>
/// <param name="Name">The organization.</param>
/// <param name="Outcome">A sentence for the confirmation page.</param>
public record OrganizationOutcome(string? Name, string Outcome);
