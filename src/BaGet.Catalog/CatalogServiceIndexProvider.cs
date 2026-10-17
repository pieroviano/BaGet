using System;
using System.Collections.Generic;
using BaGet.Core;
using BaGet.Protocol.Models;

namespace BaGet.Catalog
{
    /// <summary>
    /// Advertises Catalog/3.0.0, unless the database cannot hold the catalog.
    /// </summary>
    public class CatalogServiceIndexProvider : IServiceIndexResourceProvider
    {
        private readonly ICatalogDatabase _catalog;
        private readonly IUrlGenerator _url;

        public CatalogServiceIndexProvider(ICatalogDatabase catalog, IUrlGenerator url)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _url = url ?? throw new ArgumentNullException(nameof(url));
        }

        public IEnumerable<ServiceIndexItem> GetResources()
        {
            if (!_catalog.IsSupported) yield break;

            yield return new ServiceIndexItem
            {
                ResourceUrl = _url.GetCatalogIndexUrl(),
                Type = "Catalog/3.0.0",
            };
        }
    }
}
