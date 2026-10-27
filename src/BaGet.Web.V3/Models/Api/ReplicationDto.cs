using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web.V3.Models.Api;

/// <summary>A replication with whether a job is going on this server.</summary>
public sealed record ReplicationDto(ReplicationConfigView Replication, bool Running, string? JobNote);
