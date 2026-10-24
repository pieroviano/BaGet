using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using BaGet.Core;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Ha;

/// <summary>
/// The ASP.NET Core data protection key ring stored in the <c>DataProtectionKeys</c> table, so every node of a cluster reads and
/// writes the same keys (sign-in cookies, antiforgery tokens, protected remote credentials and replication peer keys). Used only when
/// <c>Ha:Enabled</c>; without it the host keeps its own key ring. Two nodes that start on an empty table may each add a key: the
/// key manager honours all of them.
/// </summary>
internal sealed class DatabaseXmlRepository : IXmlRepository
{
    private readonly IServiceScopeFactory _scopes;

    public DatabaseXmlRepository(IServiceScopeFactory scopes) => _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var scope = _scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();

        return context.DataProtectionKeys!
            .AsNoTracking()
            .OrderBy(k => k.Key)
            .Select(k => k.Xml)
            .ToList()
            .Where(xml => !string.IsNullOrWhiteSpace(xml))
            .Select(xml => XElement.Parse(xml!))
            .ToList();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var scope = _scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var name = friendlyName ?? string.Empty;

        context.DataProtectionKeys!.Add(new DataProtectionKeyEntry
        {
            FriendlyName = name.Length > DataProtectionKeyEntry.MaxNameLength ? name.Substring(0, DataProtectionKeyEntry.MaxNameLength) : name,
            Xml = element.ToString(SaveOptions.DisableFormatting),
        });
        context.SaveChangesAsync(default).GetAwaiter().GetResult();
    }
}
