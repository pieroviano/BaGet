using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BaGet.Web.V3.Api.Catalog;

/// <summary>One operation of the administration API, as read from the controllers.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The path template, constraints removed (<c>/api/v1/groups/{name}</c>).</param>
/// <param name="Tag">The resource the controller serves.</param>
/// <param name="Operation">The <see cref="ApiOperationAttribute"/> of the action.</param>
/// <param name="Descriptor">The action.</param>
public sealed record ApiEndpoint(string Method, string Path, string Tag, ApiOperationAttribute Operation, ControllerActionDescriptor Descriptor)
{
    /// <summary>The query parameters, route parameters and body of the action, in declaration order.</summary>
    public IReadOnlyList<ApiParameter> Parameters => ApiCatalog.ParametersOf(this);
}
