using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BaGet.Catalog;

/// <summary>
/// The Catalog/3.0.0 resource.
/// See: https://learn.microsoft.com/en-us/nuget/api/catalog-resource
/// </summary>
public class CatalogController : Controller
{
    private readonly CatalogReader _reader;

    public CatalogController(CatalogReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    /// <summary>
    /// A database that cannot hold the catalog has none, so the whole surface is absent.
    /// </summary>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!_reader.IsSupported)
        {
            context.Result = NotFound();
            return;
        }

        base.OnActionExecuting(context);
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return Json(await _reader.GetIndexAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Page(int pageIndex, CancellationToken cancellationToken)
    {
        var page = await _reader.GetPageOrNullAsync(pageIndex, cancellationToken);
        if (page == null)
        {
            return NotFound();
        }

        return Json(page);
    }

    [HttpGet]
    public async Task<IActionResult> Leaf(string commitId, CancellationToken cancellationToken)
    {
        string leaf;
        try
        {
            leaf = await _reader.GetLeafOrNullAsync(commitId, cancellationToken);
        }
        catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException)
        {
            // The entry exists but its document does not, which "catalog rebuild" cannot
            // repair: the leaf is a snapshot that was never written.
            leaf = null;
        }

        if (leaf == null)
        {
            return NotFound();
        }

        return Content(leaf, "application/json");
    }
}