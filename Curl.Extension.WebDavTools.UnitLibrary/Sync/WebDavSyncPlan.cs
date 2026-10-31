namespace Curl.Extension.WebDavTools.Sync;

/// <summary>What a sync of one listed collection does: the files to download, the sub-collections to list next,
/// the files already current and the hrefs refused.</summary>
/// <param name="Downloads">The files that are missing locally or not current, in listing order.</param>
/// <param name="SubCollections">The sub-collections to list next, in listing order.</param>
/// <param name="CurrentFiles">The files whose local copy is already current, in listing order.</param>
/// <param name="Refusals">The hrefs refused, in listing order.</param>
public sealed record WebDavSyncPlan(
    IReadOnlyList<WebDavSyncItem> Downloads,
    IReadOnlyList<WebDavSyncItem> SubCollections,
    IReadOnlyList<WebDavSyncItem> CurrentFiles,
    IReadOnlyList<WebDavSyncRefusal> Refusals);
