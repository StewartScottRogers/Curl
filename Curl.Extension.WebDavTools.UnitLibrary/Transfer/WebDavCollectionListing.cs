using System.Text;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.WebDavTools.Multistatus;
using Curl.Extension.WebDavTools.Propfind;

namespace Curl.Extension.WebDavTools.Transfer;

/// <summary>
/// One collection listed with a <c>Depth: 1</c> <c>PROPFIND</c> (RFC 4918 sections 9.1 and 10.2) sent through curl's
/// handler: the <c>207 Multistatus</c> entries (section 13), or the result the transfer fails with.
/// </summary>
/// <param name="Result">The <c>PROPFIND</c>'s result when it was read, otherwise the failure.</param>
/// <param name="Entries">One entry per <c>response</c>, or <see langword="null" /> when the listing failed.</param>
internal sealed record WebDavCollectionListing(TransferResult Result, IReadOnlyList<WebDavResourceEntry>? Entries)
{
    /// <summary>The <c>207 Multistatus</c> status code (RFC 4918 section 11.1).</summary>
    public const int MultiStatus = 207;

    /// <summary>The message of a transfer refused because its URL is not HTTP.</summary>
    public const string NotHttpMessage = "--webdav-list and --webdav-sync need an HTTP or HTTPS URL";

    /// <summary>Sends a <c>PROPFIND</c> for <paramref name="url" /> through <paramref name="next" /> and reads its body.</summary>
    /// <param name="context">The transfer the hook was given; its header outputs receive the response headers.</param>
    /// <param name="url">The collection's URL.</param>
    /// <param name="next">Runs curl's handler.</param>
    /// <returns>
    /// The entries; the handler's own result when the request failed; exit 1 when the transfer is not HTTP; exit 22 when
    /// the answer is not 207; exit 8 when the body is not a <c>multistatus</c> the reader accepts.
    /// </returns>
    public static Task<WebDavCollectionListing> ListAsync(ITransferContext context, CurlUrl url, Func<ITransferContext, ValueTask<TransferResult>> next) =>
        context.Http is null
            ? Task.FromResult(Failed(CurlExitCode.UnsupportedProtocol, NotHttpMessage))
            : PropfindAsync(context, url, next);

    private static async Task<WebDavCollectionListing> PropfindAsync(ITransferContext context, CurlUrl url, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        using MemoryStream body = new();
        var result = await next(context
            .WithRequest(url, WebDavPropfindRequest.Method, Encoding.UTF8.GetBytes(WebDavPropfindRequest.Body))
            .WithRequestHeaders([
                WebDavPropfindRequest.DepthHeaderName + ": " + WebDavPropfindRequest.DepthHeaderValue(WebDavDepth.One),
                "Content-Type: " + WebDavPropfindRequest.ContentType])
            .WithOutputs(body, context.HeaderOutput, context.DumpHeaderOutput));
        return Interpret(result, body.ToArray());
    }

    private static WebDavCollectionListing Interpret(TransferResult result, byte[] body) => result switch
    {
        { IsSuccess: false } => new(result, null),
        { Report.ResponseCode: MultiStatus } => Read(result, Encoding.UTF8.GetString(body)),
        _ => Failed(CurlExitCode.HttpReturnedError, $"The server answered PROPFIND with {result.Report?.ResponseCode ?? 0}, not 207 Multistatus"),
    };

    private static WebDavCollectionListing Read(TransferResult result, string body)
    {
        var read = WebDavMultistatusReader.Read(body);
        return read.Entries is { } entries
            ? new(result, entries)
            : Failed(CurlExitCode.WeirdServerReply, "The 207 Multistatus body was refused: " + read.Failure);
    }

    private static WebDavCollectionListing Failed(CurlExitCode exitCode, string message) => new(TransferResult.Failure(exitCode, message), null);
}
