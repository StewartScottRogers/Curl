using Curl.Extension.WebDavTools.Multistatus;

namespace Curl.Extension.WebDavTools.Sync;

/// <summary>
/// Plans the sync of one collection listed with <c>Depth: 1</c> into a local folder: each file is downloaded unless
/// its local copy is current, each sub-collection is listed next, and each href that would leave the folder is refused.
/// The sync never deletes a local file.
/// </summary>
/// <param name="fileSystem">The local file system the planner checks for current files.</param>
public sealed class WebDavSyncPlanner(IWebDavLocalFileSystem fileSystem)
{
    // A name with a path separator of any platform would reach outside the folder it maps to.
    private static readonly char[] SeparatorCharacters = ['/', '\\'];

    // The characters no file name may hold on Windows; refused on every platform so a plan is the same everywhere.
    private static readonly char[] InvalidFileNameCharacters = ['<', '>', ':', '"', '|', '?', '*'];

    /// <summary>Plans the sync of the collection at <paramref name="collectionPath"/> into <paramref name="localFolder"/>.</summary>
    /// <param name="collectionPath">The listed collection's percent-decoded path, such as <c>/dav/docs/</c>.</param>
    /// <param name="entries">The listing's entries, as <see cref="WebDavMultistatusReader"/> read them.</param>
    /// <param name="localFolder">The local folder the collection maps to.</param>
    /// <returns>The downloads, the sub-collections, the current files and the refused hrefs.</returns>
    public WebDavSyncPlan Plan(string collectionPath, IEnumerable<WebDavResourceEntry> entries, string localFolder)
    {
        ArgumentNullException.ThrowIfNull(collectionPath);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(localFolder);
        var collection = collectionPath.EndsWith('/') ? collectionPath : collectionPath + "/";
        var lists = new PlanLists([], [], [], []);
        foreach (var entry in entries)
        {
            Place(entry, collection, localFolder, lists);
        }

        return new WebDavSyncPlan(lists.Downloads, lists.SubCollections, lists.CurrentFiles, lists.Refusals);
    }

    private void Place(WebDavResourceEntry entry, string collection, string localFolder, PlanLists lists)
    {
        var path = PathOf(entry.Href);
        if (IsCollectionItself(path, collection))
        {
            return;
        }

        var refusal = RefusalOf(path, collection);
        if (refusal is not null)
        {
            lists.Refusals.Add(new WebDavSyncRefusal(entry.Href, refusal.Value));
            return;
        }

        var item = new WebDavSyncItem(entry.Href, Path.Combine(localFolder, NameOf(path, collection)), entry.LastModified);
        ListFor(entry, item.LocalPath, lists).Add(item);
    }

    // RFC 4918 section 9.1: a Depth 1 listing holds the requested collection itself, with or without its slash.
    private static bool IsCollectionItself(string path, string collection) => path == collection || path + "/" == collection;

    private List<WebDavSyncItem> ListFor(WebDavResourceEntry entry, string localPath, PlanLists lists)
    {
        if (entry.IsCollection)
        {
            return lists.SubCollections;
        }

        return IsCurrent(entry, localPath) ? lists.CurrentFiles : lists.Downloads;
    }

    // The name under the collection, without the trailing slash a collection href carries.
    private static string NameOf(string path, string collection)
    {
        var name = path[collection.Length..];
        return name.EndsWith('/') ? name[..^1] : name;
    }

    // An href is a path or an absolute URL (RFC 4918 section 8.3); the URL's scheme and authority are dropped.
    private static string PathOf(string href)
    {
        var schemeEnd = href.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0)
        {
            return href;
        }

        var pathStart = href.IndexOf('/', schemeEnd + 3);
        return pathStart < 0 ? "/" : href[pathStart..];
    }

    private static WebDavSyncRefusalKind? RefusalOf(string path, string collection)
    {
        if (!path.StartsWith(collection, StringComparison.Ordinal))
        {
            return WebDavSyncRefusalKind.OutsideCollection;
        }

        var name = NameOf(path, collection);
        if (IsTraversal(name))
        {
            return WebDavSyncRefusalKind.TraversalSegment;
        }

        return IsInvalidFileName(name) ? WebDavSyncRefusalKind.InvalidFileName : null;
    }

    private static bool IsTraversal(string name) => name == "." || name == ".." || name.IndexOfAny(SeparatorCharacters) >= 0;

    private static bool IsInvalidFileName(string name) =>
        name.Length == 0 || name.IndexOfAny(InvalidFileNameCharacters) >= 0 || name.Any(char.IsControl);
    // RFC 4918 sections 15.4 and 15.7: current when the local file has the listed length and was written no earlier
    // than the listed last-modified time. A listing that leaves either out cannot show the copy current.
    private bool IsCurrent(WebDavResourceEntry entry, string localPath)
    {
        var local = fileSystem.FindFile(localPath);
        return local is not null
            && entry.ContentLength == local.Length
            && entry.LastModified is { } lastModified
            && local.LastWriteTime >= lastModified;
    }

    private sealed record PlanLists(
        List<WebDavSyncItem> Downloads,
        List<WebDavSyncItem> SubCollections,
        List<WebDavSyncItem> CurrentFiles,
        List<WebDavSyncRefusal> Refusals);
}
