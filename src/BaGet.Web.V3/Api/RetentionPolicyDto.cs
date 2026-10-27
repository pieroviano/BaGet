using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Web.V3.Api;

/// <summary>A policy with its scope's name and the last run.</summary>
public sealed record RetentionPolicyDto(RetentionPolicyView Policy, RetentionJobStatus? LastRun);
