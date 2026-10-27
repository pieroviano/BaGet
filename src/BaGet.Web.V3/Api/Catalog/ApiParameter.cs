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

/// <summary>A parameter of an operation.</summary>
/// <param name="Name">The name on the wire.</param>
/// <param name="In"><c>path</c>, <c>query</c> or <c>body</c>.</param>
/// <param name="Type">The CLR type.</param>
/// <param name="Required">Whether it must be given.</param>
public sealed record ApiParameter(string Name, string In, Type Type, bool Required);
