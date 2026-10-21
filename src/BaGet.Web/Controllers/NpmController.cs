using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace BaGet.Web;

/// <summary>Reads the package name an npm route carries.</summary>
public static class NpmRouteNames
{
    /// <summary>
    /// The raw package name of an npm request: <c>name</c> (which may still hold the encoded <c>@scope%2fname</c>), or
    /// <c>@{scope}/{pkg}</c> of a route that carries the scope as its own segment; null for a route without a package.
    /// Validate it with <see cref="NpmNames.TryParse"/>.
    /// </summary>
    public static string? FromRouteValues(RouteValueDictionary values)
    {
        if (values.TryGetValue("name", out var name) && name is string n) return n;
        if (values.TryGetValue("scope", out var scope) && scope is string s && values.TryGetValue("pkg", out var pkg) && pkg is string p)
        {
            return "@" + s + "/" + p;
        }

        return null;
    }
}

/// <summary>
/// The npm registry protocol of a repository (<c>npm/…</c>): packuments, tarballs, publish, dist-tags, deprecate, unpublish,
/// search, ping, whoami and the login calls. Every route is registered through <see cref="BaGetEndpointBuilder.MapRoute"/> with
/// the npm format, so a request in a repository of another format is a 404. Reads (GET, and the audit POSTs) pass
/// <see cref="RepositoryReadFilter"/>; write routes are constrained to their methods and authorize themselves through
/// <see cref="INpmRegistry"/> with the caller <see cref="RequestPrincipalResolver.ResolveForWriteAsync"/> found (an API key,
/// never the browser session). Bodies are JSON; errors are <c>{"error": "…"}</c>, which is what npm prints.
/// </summary>
public class NpmController : Controller
{
    private const string AbbreviatedType = "application/vnd.npm.install-v1+json";

    private readonly INpmRegistry _registry;
    private readonly RequestPrincipalResolver _principals;
    private readonly IRepositoryContext _repository;
    private readonly IOptionsSnapshot<BaGetOptions> _options;
    private readonly IOptionsSnapshot<NpmOptions> _npmOptions;
    private readonly IUrlGenerator _urls;
    private readonly INpmTokenService? _tokens;

    public NpmController(
        INpmRegistry registry,
        RequestPrincipalResolver principals,
        IRepositoryContext repository,
        IOptionsSnapshot<BaGetOptions> options,
        IOptionsSnapshot<NpmOptions> npmOptions,
        IUrlGenerator urls,
        INpmTokenService? tokens = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _principals = principals ?? throw new ArgumentNullException(nameof(principals));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _npmOptions = npmOptions ?? throw new ArgumentNullException(nameof(npmOptions));
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
        _tokens = tokens;
    }

    // ---- helpers --------------------------------------------------------------------------------------------------------

    private string? RawName => NpmRouteNames.FromRouteValues(RouteData.Values);

    private bool TryName(out string name, out IActionResult? failure)
    {
        failure = null;
        if (NpmNames.TryParse(RawName, out name, out var error)) return true;

        failure = Error(400, error);
        return false;
    }

    private IActionResult Error(int status, string message) => new ContentResult
    {
        StatusCode = status,
        ContentType = "application/json",
        Content = new JsonObject { ["error"] = message }.ToJsonString(),
    };

    private IActionResult Json(int status, JsonNode body) => new ContentResult
    {
        StatusCode = status,
        ContentType = "application/json",
        Content = body.ToJsonString(),
    };

    private IActionResult Respond(NpmResult result)
    {
        switch (result.Status)
        {
            case NpmStatus.Ok:
            case NpmStatus.Created:
                var body = new JsonObject { ["ok"] = true, ["success"] = true };
                if (result.Message != null) body["message"] = result.Message;

                return Json(result.Status == NpmStatus.Created ? 201 : 200, body);

            case NpmStatus.Unauthorized:
                return Challenge401(result.Message ?? "Authentication is required.");

            case NpmStatus.Forbidden:
                return Error(403, result.Message ?? "Forbidden.");

            case NpmStatus.NotFound:
                return Error(404, result.Message ?? "Not found.");

            case NpmStatus.Conflict:
                return Error(409, result.Message ?? "Conflict.");

            default:
                return Error(400, result.Message ?? "The request is not valid.");
        }
    }

    private IActionResult Challenge401(string message)
    {
        Response.Headers.WWWAuthenticate = new Microsoft.Extensions.Primitives.StringValues(new[] { BasicCredentials.BearerChallenge, BasicCredentials.Challenge });

        return Error(401, message);
    }

    /// <summary>The caller of a write, or the failure to return: 401 without valid credentials, 403 in read-only mode.</summary>
    private async Task<(BaGetPrincipal? Caller, IActionResult? Failure)> CallerAsync(CancellationToken cancellationToken)
    {
        if (_options.Value.IsReadOnlyMode) return (null, Error(403, "This server is read only."));

        var caller = await _principals.ResolveForWriteAsync(HttpContext, cancellationToken);

        return caller == null ? (null, Challenge401("Authentication is required.")) : (caller, null);
    }

    // ---- reads ----------------------------------------------------------------------------------------------------------

    /// <summary><c>GET npm/-/ping</c>: an empty object, for <c>npm ping</c> and health checks.</summary>
    [HttpGet]
    public IActionResult Ping() => Json(200, new JsonObject());

    /// <summary><c>GET npm/-/whoami</c>: the user name behind the token, 401 without one.</summary>
    [HttpGet]
    public async Task<IActionResult> Whoami(CancellationToken cancellationToken)
    {
        var caller = await _principals.ResolveForWriteAsync(HttpContext, cancellationToken);
        if (caller == null) return Challenge401("Authentication is required.");

        return Json(200, new JsonObject { ["username"] = caller.UserName ?? "superuser" });
    }

    /// <summary><c>GET npm/{name}</c>: the packument (the abbreviated install document when the client asks for it).</summary>
    [HttpGet]
    public async Task<IActionResult> Packument(CancellationToken cancellationToken)
    {
        if (!TryName(out var name, out _)) return Error(404, "Not found");

        var abbreviated = Request.Headers.Accept.ToString().Contains(AbbreviatedType, StringComparison.OrdinalIgnoreCase);
        var packument = await _registry.GetPackumentAsync(name, abbreviated, cancellationToken);
        if (packument == null) return Error(404, "Not found");

        Response.Headers.Vary = "Accept";

        return new ContentResult
        {
            StatusCode = 200,
            ContentType = abbreviated ? AbbreviatedType : "application/json",
            Content = packument.ToJsonString(),
        };
    }

    /// <summary><c>GET npm/{name}/-/{file}</c>: the tarball.</summary>
    [HttpGet]
    public async Task<IActionResult> Tarball(string file, CancellationToken cancellationToken)
    {
        if (!TryName(out var name, out _)) return Error(404, "Not found");

        var tarball = await _registry.OpenTarballAsync(name, file, cancellationToken);
        if (tarball == null) return Error(404, "Not found");

        Response.Headers.ETag = "\"" + tarball.Integrity + "\"";

        return File(tarball.Content, "application/octet-stream");
    }

    /// <summary><c>GET npm/-/package/{name}/dist-tags</c>.</summary>
    [HttpGet]
    public async Task<IActionResult> DistTags(CancellationToken cancellationToken)
    {
        if (!TryName(out var name, out _)) return Error(404, "Not found");

        var tags = await _registry.GetDistTagsAsync(name, cancellationToken);
        if (tags == null) return Error(404, "Not found");

        var body = new JsonObject();
        foreach (var tag in tags.OrderBy(t => t.Key, StringComparer.Ordinal)) body[tag.Key] = tag.Value;

        return Json(200, body);
    }

    /// <summary><c>GET npm/-/v1/search?text=&amp;size=&amp;from=</c>: packages whose name contains the text.</summary>
    [HttpGet]
    public async Task<IActionResult> Search(string? text, int size = 20, int from = 0, CancellationToken cancellationToken = default)
    {
        var page = await _registry.SearchAsync(text, Math.Max(from, 0), Math.Clamp(size, 1, 250), cancellationToken);
        var registry = _urls.GetNpmRegistryUrl();

        var objects = new JsonArray();
        foreach (var hit in page.Hits)
        {
            var scope = NpmNames.Scope(hit.Name);
            var package = new JsonObject
            {
                ["name"] = hit.Name,
                ["scope"] = scope == null ? "unscoped" : scope.TrimStart('@'),
                ["version"] = hit.Version,
                ["description"] = hit.Description,
                ["keywords"] = new JsonArray(hit.Keywords.Select(k => (JsonNode?)JsonValue.Create(k)).ToArray()),
                ["date"] = hit.Modified.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture),
                ["links"] = new JsonObject { ["npm"] = registry + hit.Name },
            };

            objects.Add(new JsonObject
            {
                ["package"] = package,
                ["score"] = new JsonObject
                {
                    ["final"] = 1,
                    ["detail"] = new JsonObject { ["quality"] = 1, ["popularity"] = 1, ["maintenance"] = 1 },
                },
                ["searchScore"] = 1,
            });
        }

        return Json(200, new JsonObject { ["objects"] = objects, ["total"] = page.Total, ["time"] = DateTime.UtcNow.ToString("R", System.Globalization.CultureInfo.InvariantCulture) });
    }

    /// <summary>
    /// <c>POST npm/-/npm/v1/security/advisories/bulk</c> and <c>…/audits/quick</c>: the audit calls <c>npm install</c> makes
    /// unless told not to. This registry keeps no advisories, so the answer is "none" (vulnerability scanning is a later phase);
    /// the request body is not read.
    /// </summary>
    [HttpPost]
    public IActionResult AuditBulk() => Json(200, new JsonObject());

    /// <summary>See <see cref="AuditBulk"/>.</summary>
    [HttpPost]
    public IActionResult AuditQuick() => Json(200, new JsonObject
    {
        ["actions"] = new JsonArray(),
        ["advisories"] = new JsonObject(),
        ["muted"] = new JsonArray(),
        ["metadata"] = new JsonObject
        {
            ["vulnerabilities"] = new JsonObject { ["info"] = 0, ["low"] = 0, ["moderate"] = 0, ["high"] = 0, ["critical"] = 0 },
            ["dependencies"] = 0,
            ["devDependencies"] = 0,
            ["optionalDependencies"] = 0,
            ["totalDependencies"] = 0,
        },
    });

    // ---- login ----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>POST npm/-/v1/login</c>: npm's web login. Not offered, so npm falls back to <see cref="Login"/> (the answer must be 404).
    /// </summary>
    [HttpPost]
    public IActionResult WebLogin() => Error(404, "Web login is not supported; use the user name and password.");

    /// <summary>
    /// <c>PUT npm/-/user/org.couchdb.user:{name}</c>: <c>npm login</c> and <c>npm adduser</c>. Checks the account's password and
    /// answers with a token, an API key of the user limited to this repository. It never creates an account.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> Login(string userKey, CancellationToken cancellationToken)
    {
        if (_tokens == null)
        {
            return Error(501, "This server has no accounts: use its API key as the _authToken.");
        }

        const string prefix = "org.couchdb.user:";
        var routeName = userKey != null && userKey.StartsWith(prefix, StringComparison.Ordinal) ? Uri.UnescapeDataString(userKey.Substring(prefix.Length)) : null;
        if (string.IsNullOrEmpty(routeName)) return Error(400, "The user is not valid.");

        JsonObject? body;
        try
        {
            body = await ReadJsonAsync(64 * 1024, cancellationToken) as JsonObject;
        }
        catch (Exception e) when (e is JsonException or InvalidDataException)
        {
            return Error(400, "The request is not valid JSON.");
        }

        var userName = body?["name"]?.GetValue<string>();
        var password = body?["password"]?.GetValue<string>();
        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password) || !string.Equals(userName, routeName, StringComparison.OrdinalIgnoreCase))
        {
            return Error(400, "A user name and a password are required.");
        }

        var result = await _tokens.LoginAsync(userName, password, _repository.Current, cancellationToken);
        if (result.Token == null)
        {
            Response.Headers.WWWAuthenticate = BasicCredentials.BearerChallenge;
            return Error(result.LockedOut ? 429 : 401, result.Error ?? "Bad username or password.");
        }

        return Json(201, new JsonObject
        {
            ["ok"] = true,
            ["id"] = prefix + userName,
            ["rev"] = "_we_dont_use_revs_any_more",
            ["name"] = userName,
            ["token"] = result.Token,
        });
    }

    /// <summary><c>DELETE npm/-/user/token/{token}</c>: <c>npm logout</c> revokes the token it was given.</summary>
    [HttpDelete]
    public async Task<IActionResult> Logout(string token, CancellationToken cancellationToken)
    {
        var (caller, failure) = await CallerAsync(cancellationToken);
        if (failure != null) return failure;

        // A global key is not a stored token: logging out of it only forgets it on the client.
        if (_tokens != null && caller!.UserId != null) await _tokens.RevokeAsync(caller, Uri.UnescapeDataString(token), cancellationToken);

        return Json(200, new JsonObject { ["ok"] = true });
    }

    // ---- publish, deprecate, unpublish ----------------------------------------------------------------------------------

    /// <summary>
    /// <c>PUT npm/{name}</c>: <c>npm publish</c> (the body carries the new version and its tarball) or <c>npm deprecate</c>
    /// (the whole packument with changed <c>deprecated</c> fields, no attachment).
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> Put(CancellationToken cancellationToken)
    {
        var (caller, failure) = await CallerAsync(cancellationToken);
        if (failure != null) return failure;
        if (!TryName(out var name, out var invalid)) return invalid!;

        var limit = _npmOptions.Value.MaxPublishBytes;
        if (limit > 0 && Request.ContentLength > limit) return Error(413, $"The request is larger than {limit} bytes.");

        NpmPublishDocument document;
        try
        {
            document = await NpmPublishBody.ParseAsync(Request.Body, limit, cancellationToken);
        }
        catch (NpmBodyTooLargeException e)
        {
            return Error(413, e.Message);
        }
        catch (InvalidDataException e)
        {
            return Error(400, e.Message);
        }

        using (document)
        {
            var root = document.Root;
            if (root["name"] is JsonValue declared && declared.TryGetValue<string>(out var declaredName)
                && !string.Equals(declaredName, name, StringComparison.Ordinal))
            {
                return Error(400, $"The document is for {declaredName}, not {name}.");
            }

            return document.Attachments.Count > 0
                ? await PublishAsync(caller, name, document, cancellationToken)
                : await UpdateMetadataAsync(caller, name, root, cancellationToken);
        }
    }

    private async Task<IActionResult> PublishAsync(BaGetPrincipal? caller, string name, NpmPublishDocument document, CancellationToken cancellationToken)
    {
        var root = document.Root;
        if (document.Attachments.Count != 1) return Error(400, "A publish carries exactly one tarball.");
        if (root["versions"] is not JsonObject versions || versions.Count != 1) return Error(400, "A publish carries exactly one version.");

        var attachment = document.Attachments[0];
        var (version, manifestNode) = versions.First();
        if (manifestNode is not JsonObject manifest) return Error(400, "The version is not an object.");

        // The attachment is named name-version.tgz; with a scope the client repeats it (@scope/name-1.0.0.tgz).
        if (attachment.Name != $"{name}-{version}.tgz" && attachment.Name != NpmNames.TarballFileName(name, version))
        {
            return Error(400, $"The attachment {attachment.Name} is not the tarball of {name}@{version}.");
        }

        var tag = (root["dist-tags"] as JsonObject)?.FirstOrDefault(t => t.Value is JsonValue v && v.TryGetValue<string>(out var tagged) && tagged == version).Key;
        if (tag == null) return Error(400, "You must specify a tag using --tag.");

        if (manifest["readme"] == null && root["readme"] is JsonValue readme) manifest["readme"] = readme.DeepClone();

        var dist = manifest["dist"] as JsonObject;
        string? integrity = dist?["integrity"] is JsonValue i && i.TryGetValue<string>(out var integrityText) ? integrityText : null;
        string? shasum = dist?["shasum"] is JsonValue s && s.TryGetValue<string>(out var shasumText) ? shasumText : null;

        await using var tarball = attachment.Open();
        var result = await _registry.PublishAsync(
            caller,
            new NpmPublication(name, version, manifest, tag, tarball, integrity, shasum, attachment.DeclaredLength),
            cancellationToken);

        return Respond(result);
    }

    private async Task<IActionResult> UpdateMetadataAsync(BaGetPrincipal? caller, string name, JsonObject root, CancellationToken cancellationToken)
    {
        // Without a tarball the only change a packument PUT may make is a version's deprecation (npm deprecate); dist-tags have
        // their own routes and nothing else is taken from the document.
        if (root["versions"] is not JsonObject versions) return Error(400, "The document has no versions.");

        var package = await _registry.GetPackageAsync(name, cancellationToken);
        if (package == null) return Error(404, "Not found");

        var changes = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (version, node) in versions)
        {
            var stored = package.Versions.FirstOrDefault(v => v.Version == version);
            if (stored == null) return Error(400, $"{name} has no version {version}: a new version needs its tarball.");

            var message = node is JsonObject manifest && manifest["deprecated"] is JsonValue d && d.TryGetValue<string>(out var text) ? text : null;
            message = string.IsNullOrWhiteSpace(message) ? null : message;
            if (stored.Deprecated != message) changes[version] = message;
        }

        if (changes.Count == 0)
        {
            // Authenticated, nothing to change.
            return Json(200, new JsonObject { ["ok"] = true, ["success"] = true });
        }

        return Respond(await _registry.DeprecateAsync(caller, name, changes, cancellationToken));
    }

    /// <summary>
    /// <c>PUT npm/{name}/-rev/{rev}</c>: <c>npm unpublish name@version</c> sends the packument without the version (the first
    /// step; the tarball is deleted by <see cref="DeleteTarball"/> next). The versions missing from the document are unpublished,
    /// and its <c>dist-tags</c> say where the tags that pointed at them go.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> PutRevision(string rev, CancellationToken cancellationToken)
    {
        var (caller, failure) = await CallerAsync(cancellationToken);
        if (failure != null) return failure;
        if (!TryName(out var name, out var invalid)) return invalid!;

        JsonObject? root;
        try
        {
            root = await ReadJsonAsync(_npmOptions.Value.MaxPublishBytes, cancellationToken) as JsonObject;
        }
        catch (Exception e) when (e is JsonException or InvalidDataException)
        {
            return Error(400, "The request is not valid JSON.");
        }

        if (root?["versions"] is not JsonObject kept) return Error(400, "The document has no versions.");

        var package = await _registry.GetPackageAsync(name, cancellationToken);
        if (package == null) return Error(404, "Not found");

        var removed = package.Versions.Select(v => v.Version).Where(v => !kept.ContainsKey(v)).ToList();
        if (removed.Count == 0) return Json(200, new JsonObject { ["ok"] = true, ["success"] = true });

        var tags = (root["dist-tags"] as JsonObject)?
            .Where(t => t.Value is JsonValue)
            .ToDictionary(t => t.Key, t => t.Value!.GetValue<string>(), StringComparer.Ordinal);

        return Respond(await _registry.UnpublishVersionsAsync(caller, name, removed, tags, cancellationToken));
    }

    /// <summary><c>DELETE npm/{name}/-rev/{rev}</c>: <c>npm unpublish name --force</c> removes the whole package.</summary>
    [HttpDelete]
    public async Task<IActionResult> DeletePackage(string rev, CancellationToken cancellationToken)
    {
        var (caller, failure) = await CallerAsync(cancellationToken);
        if (failure != null) return failure;
        if (!TryName(out var name, out var invalid)) return invalid!;

        return Respond(await _registry.UnpublishPackageAsync(caller, name, cancellationToken));
    }

    /// <summary><c>DELETE npm/{name}/-/{file}/-rev/{rev}</c>: deletes a tarball (the last step of <c>npm unpublish</c>).</summary>
    [HttpDelete]
    public async Task<IActionResult> DeleteTarball(string file, string rev, CancellationToken cancellationToken)
    {
        var (caller, failure) = await CallerAsync(cancellationToken);
        if (failure != null) return failure;
        if (!TryName(out var name, out var invalid)) return invalid!;

        return Respond(await _registry.DeleteTarballAsync(caller, name, file, cancellationToken));
    }

    // ---- dist-tags ------------------------------------------------------------------------------------------------------

    /// <summary><c>PUT npm/-/package/{name}/dist-tags/{tag}</c>: the body is the version as a JSON string.</summary>
    [HttpPut]
    public async Task<IActionResult> SetDistTag(string tag, CancellationToken cancellationToken)
    {
        var (caller, failure) = await CallerAsync(cancellationToken);
        if (failure != null) return failure;
        if (!TryName(out var name, out var invalid)) return invalid!;

        string? version;
        try
        {
            var body = await ReadJsonAsync(16 * 1024, cancellationToken);
            version = body is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
        }
        catch (Exception e) when (e is JsonException or InvalidDataException)
        {
            return Error(400, "The request is not valid JSON.");
        }

        if (string.IsNullOrEmpty(version)) return Error(400, "The body is the version, as a JSON string.");

        return Respond(await _registry.SetDistTagAsync(caller, name, Uri.UnescapeDataString(tag), version, cancellationToken));
    }

    /// <summary><c>DELETE npm/-/package/{name}/dist-tags/{tag}</c>.</summary>
    [HttpDelete]
    public async Task<IActionResult> RemoveDistTag(string tag, CancellationToken cancellationToken)
    {
        var (caller, failure) = await CallerAsync(cancellationToken);
        if (failure != null) return failure;
        if (!TryName(out var name, out var invalid)) return invalid!;

        return Respond(await _registry.RemoveDistTagAsync(caller, name, Uri.UnescapeDataString(tag), cancellationToken));
    }

    private async Task<JsonNode?> ReadJsonAsync(long limit, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var bytes = new byte[16 * 1024];
        int read;
        while ((read = await Request.Body.ReadAsync(bytes, cancellationToken)) > 0)
        {
            if (limit > 0 && buffer.Length + read > limit) throw new InvalidDataException("The request body is too large.");

            buffer.Write(bytes, 0, read);
        }

        buffer.Position = 0;

        return buffer.Length == 0 ? null : JsonNode.Parse(buffer);
    }
}
