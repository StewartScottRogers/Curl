using System.Globalization;
using System.Text;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.WebDavTools.Multistatus;

namespace Curl.Extension.WebDavTools.Transfer;

/// <summary>
/// The <c>--webdav-list</c> transfer hook (ADR-1149): sends a <c>Depth: 1</c> <c>PROPFIND</c> for the transfer's URL in
/// place of its own request and writes one line per <c>response</c> of the <c>207 Multistatus</c> body, in body order:
/// <c>collection</c> or <c>file</c>, the length, the last-modified date (RFC 1123), and the percent-decoded href,
/// separated by tabs, with <c>-</c> for a property the server did not report.
/// </summary>
internal sealed class WebDavListTransferHook : ITransferHook
{
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        var listing = await WebDavCollectionListing.ListAsync(context, context.Url, next);
        if (listing.Entries is { } entries)
        {
            await context.Output.WriteAsync(Encoding.UTF8.GetBytes(string.Concat(entries.Select(LineOf))), context.CancellationToken);
        }

        return listing.Result;
    }

    /// <summary>Gives the listing line of <paramref name="entry" />, newline included.</summary>
    public static string LineOf(WebDavResourceEntry entry) =>
        string.Join(
            '\t',
            entry.IsCollection ? "collection" : "file",
            entry.ContentLength?.ToString(CultureInfo.InvariantCulture) ?? "-",
            entry.LastModified?.ToString("r", CultureInfo.InvariantCulture) ?? "-",
            entry.Href) + "\n";
}
