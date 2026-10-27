using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web.V3.Api;

/// <summary>The JSON the API writes: camelCase, enums as their names, nulls left out.</summary>
public static class ApiJson
{
    /// <summary>The options of every API response (the protocol endpoints keep the host's own).</summary>
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}

/// <summary>A page of a list: <c>?page=</c> (from 1) and <c>?pageSize=</c> (at most <see cref="MaxPageSize"/>).</summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">This page.</param>
/// <param name="Page">The page number, from 1.</param>
/// <param name="PageSize">The size asked for.</param>
/// <param name="Total">How many items the list has.</param>
public sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    /// <summary>The largest page size.</summary>
    public const int MaxPageSize = 200;

    /// <summary>The page size when none is asked for.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>Whether another page follows.</summary>
    public bool HasMore => (long)Page * PageSize < Total;
}

/// <summary>Builds the RFC 9457 problem answers of the API (<c>application/problem+json</c>).</summary>
public static class ApiProblems
{
    /// <summary>Base of the problem <c>type</c> URIs.</summary>
    public const string TypeBase = "https://baget.dev/problems/";

    /// <summary>A problem result with the given status, short title, detail and a stable <paramref name="code"/> (the last part of <c>type</c>).</summary>
    public static ObjectResult Create(int status, string code, string title, string? detail = null, IDictionary<string, object?>? extensions = null)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail, Type = TypeBase + code };
        problem.Extensions["code"] = code;
        if (extensions != null)
        {
            foreach (var pair in extensions) problem.Extensions[pair.Key] = pair.Value;
        }

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }

    /// <summary>404.</summary>
    public static ObjectResult NotFound(string what) => Create(404, "not-found", "Not found", $"There is no {what}.");

    /// <summary>400.</summary>
    public static ObjectResult BadRequest(string detail) => Create(400, "invalid-request", "Invalid request", detail);

    /// <summary>409.</summary>
    public static ObjectResult Conflict(string detail) => Create(409, "conflict", "Conflict", detail);

    /// <summary>The page number and size from the query, clamped.</summary>
    public static (int Page, int PageSize) Clamp(int? page, int? pageSize)
        => (Math.Max(1, page ?? 1), Math.Clamp(pageSize ?? Paged<int>.DefaultPageSize, 1, Paged<int>.MaxPageSize));
}
