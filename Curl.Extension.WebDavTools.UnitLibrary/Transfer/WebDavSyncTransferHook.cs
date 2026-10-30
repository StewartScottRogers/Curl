using System.Text;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.WebDavTools.Sync;

namespace Curl.Extension.WebDavTools.Transfer;

/// <summary>
/// The <c>--webdav-sync &lt;dir&gt;</c> transfer hook (ADR-1149): lists the transfer's URL with a <c>Depth: 1</c>
/// <c>PROPFIND</c>, plans it with <see cref="WebDavSyncPlanner" />, downloads each file that is not current with a
/// <c>GET</c> into a <c>.webdav-part</c> file renamed over the local copy once the response is a 2xx, sets its last-write
/// time to <c>getlastmodified</c> (RFC 4918 section 15.7), and syncs each sub-collection into its own folder the same
/// way. It writes one tab-separated line per item to the output: <c>refused</c> and the href, <c>current</c> or
/// <c>downloaded</c> and the local path. The first failure ends the sync with its result; it never deletes a local file.
/// </summary>
/// <param name="localFolder">The local folder the transfer's collection maps to.</param>
/// <param name="fileSystem">The local file system the planner checks for current files.</param>
internal sealed class WebDavSyncTransferHook(string localFolder, IWebDavLocalFileSystem fileSystem) : ITransferHook
{
    /// <summary>The suffix of the file a download is written to before it replaces the local copy.</summary>
    public const string PartialFileSuffix = ".webdav-part";

    /// <summary>Gets the local folder the transfer's collection maps to.</summary>
    public string LocalFolder => localFolder;

    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        try
        {
            return await SyncAsync(context, context.Url, localFolder, next);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return TransferResult.Failure(CurlExitCode.WriteError, "--webdav-sync could not write to the local folder: " + exception.Message);
        }
    }

    /// <summary>Gives the URL of the member <paramref name="item" /> of the collection at <paramref name="collection" />.</summary>
    /// <param name="collection">The collection's URL; its query and fragment are dropped.</param>
    /// <param name="item">The member, whose local file name is its name in the collection.</param>
    /// <param name="isCollection">Whether the member is a collection, whose URL ends in <c>/</c>.</param>
    /// <returns>The member's URL, its name percent-encoded.</returns>
    public static CurlUrl MemberUrl(CurlUrl collection, WebDavSyncItem item, bool isCollection)
    {
        var text = collection.OriginalString;
        var queryOrFragment = text.IndexOfAny(['?', '#']);
        var path = (queryOrFragment < 0 ? text : text[..queryOrFragment]).TrimEnd('/');
        return CurlUrl.Parse(path + "/" + Uri.EscapeDataString(Path.GetFileName(item.LocalPath)) + (isCollection ? "/" : ""));
    }

    private async Task<TransferResult> SyncAsync(ITransferContext context, CurlUrl url, string folder, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        var listing = await WebDavCollectionListing.ListAsync(context, url, next);
        if (listing.Entries is not { } entries)
        {
            return listing.Result;
        }

        var plan = new WebDavSyncPlanner(fileSystem).Plan(Uri.UnescapeDataString(url.AbsolutePath), entries, folder);
        Directory.CreateDirectory(folder);
        foreach (var refusal in plan.Refusals)
        {
            await WriteLineAsync(context, "refused", refusal.Href);
        }

        foreach (var current in plan.CurrentFiles)
        {
            await WriteLineAsync(context, "current", current.LocalPath);
        }

        return await DownloadAllAsync(context, url, plan, next) ?? await SyncSubCollectionsAsync(context, url, plan, next) ?? listing.Result;
    }

    private static async Task<TransferResult?> DownloadAllAsync(ITransferContext context, CurlUrl url, WebDavSyncPlan plan, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        foreach (var download in plan.Downloads)
        {
            var result = await DownloadAsync(context, MemberUrl(url, download, false), download, next);
            if (!result.IsSuccess)
            {
                return result;
            }

            await WriteLineAsync(context, "downloaded", download.LocalPath);
        }

        return null;
    }

    private async Task<TransferResult?> SyncSubCollectionsAsync(ITransferContext context, CurlUrl url, WebDavSyncPlan plan, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        foreach (var subCollection in plan.SubCollections)
        {
            var result = await SyncAsync(context, MemberUrl(url, subCollection, true), subCollection.LocalPath, next);
            if (!result.IsSuccess)
            {
                return result;
            }
        }

        return null;
    }

    private static async Task<TransferResult> DownloadAsync(ITransferContext context, CurlUrl url, WebDavSyncItem item, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        var partialPath = item.LocalPath + PartialFileSuffix;
        var result = await FetchAsync(context, url, partialPath, next);
        return Keep(result, item, partialPath);
    }

    private static async Task<TransferResult> FetchAsync(ITransferContext context, CurlUrl url, string partialPath, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        await using var partial = new FileStream(partialPath, FileMode.Create, FileAccess.Write);
        return await next(context.WithRequest(url, "GET", null).WithOutputs(partial, context.HeaderOutput, context.DumpHeaderOutput));
    }

    // Renames the partial file over the local copy when the GET was answered with a 2xx; otherwise deletes it.
    private static TransferResult Keep(TransferResult result, WebDavSyncItem item, string partialPath)
    {
        var failure = result switch
        {
            { IsSuccess: false } => result,
            { Report.ResponseCode: >= 200 and < 300 } => null,
            _ => TransferResult.Failure(CurlExitCode.HttpReturnedError, $"The server answered GET {item.Href} with {result.Report?.ResponseCode ?? 0}"),
        };
        if (failure is not null)
        {
            File.Delete(partialPath);
            return failure;
        }

        File.Move(partialPath, item.LocalPath, overwrite: true);
        if (item.LastModified is { } lastModified)
        {
            File.SetLastWriteTimeUtc(item.LocalPath, lastModified.UtcDateTime);
        }

        return result;
    }

    private static async Task WriteLineAsync(ITransferContext context, string kind, string text) =>
        await context.Output.WriteAsync(Encoding.UTF8.GetBytes(kind + "\t" + text + "\n"), context.CancellationToken);
}
