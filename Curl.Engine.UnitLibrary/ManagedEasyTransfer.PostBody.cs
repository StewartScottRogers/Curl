using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Proxy;
using Curl.Core;
using Curl.Core.Hsts;
using Curl.Core.Multipart;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http;
using Curl.Original.Protocol.Ws;

namespace Curl.Engine;

// Building the POST body of a perform: MIME forms, read callbacks and trailers.
internal sealed partial class ManagedEasyTransfer
{
    /// <summary>
    /// Builds the <c>CURLOPT_MIMEPOST</c> body (<see cref="MimePostFormUnlessToolGaveAsync" />) and runs the transfer
    /// with it, or ends the perform with the build's failure before anything is set up or sent.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="settings">The tool settings.</param>
    /// <param name="url">The URL set, parsed.</param>
    /// <param name="urlText">The URL set.</param>
    /// <param name="transferId">The transfer's <c>%{xfer_id}</c>.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The transfer's code.</returns>
    private async Task<CurlCode> PerformBuildingMimePostAsync(
        EasyOptions options,
        EngineTransferSettings settings,
        CurlUrl url,
        string urlText,
        long transferId,
        CancellationToken cancellationToken)
    {
        EngineToolTransferInputs? toolInputs = ToolInputs;
        List<CallbackReadStream> mimeReads = [];
        MultipartFormBuildResult? form = await MimePostFormUnlessToolGaveAsync(toolInputs, options, url, mimeReads, cancellationToken).ConfigureAwait(false);
        return form is { IsBuilt: false }
            ? Finish(form.Failure.ExitCode.ToCurlCode(), form.Failure.ErrorMessage!, Info)
            : await PerformDisposingFormAsync(options, settings, url, urlText, transferId, toolInputs, form?.Body ?? ReadCallbackPostBodyOf(toolInputs, options, url, mimeReads) ?? TrailerPostFieldsBodyOf(toolInputs, options, url), mimeReads, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The body of a <c>CURLOPT_POST</c> with no <c>CURLOPT_POSTFIELDS</c>, read through <c>CURLOPT_READFUNCTION</c>
    /// (<see cref="CallbackUploadOf" />) and sent as <c>application/x-www-form-urlencoded</c>, with
    /// <c>CURLOPT_POSTFIELDSIZE</c> as its <c>Content-Length</c> when not negative and chunked otherwise, as libcurl
    /// 8.22.0 posts (upstream test508, BL-2586), and chunked whatever the size when a <c>CURLOPT_HTTPHEADER</c> line
    /// asks for <c>Transfer-Encoding: chunked</c> (<see cref="AsksForChunked" />, upstream test1901, BL-2667); the stream joins <paramref name="reads" />, so its
    /// <c>CURL_READFUNC_ABORT</c> ends the perform. With no read callback and a <c>CURLOPT_POSTFIELDSIZE</c> of 0
    /// (<c>CURLOPT_POSTFIELDS</c> NULL), an empty body sent with <c>Content-Length: 0</c>, as libcurl 8.22.0 posts
    /// (upstream test515, BL-2592), and any other size an empty chunked body, as libcurl 8.22.0 reads its default
    /// read callback on an empty stdin (BL-3319). None when the curl tool's runner gave <paramref name="toolInputs" />
    /// or for a <c>CURLOPT_UPLOAD</c>.
    /// </summary>
    /// <param name="toolInputs">The runner's chosen pieces, or <see langword="null" /> for a handle's own.</param>
    /// <param name="options">The handle's options.</param>
    /// <param name="url">The URL set, whose scheme says whether the transfer can pause.</param>
    /// <param name="reads">Receives the body's stream.</param>
    /// <returns>The body, or <see langword="null" /> for none.</returns>
    private StreamBody? ReadCallbackPostBodyOf(EngineToolTransferInputs? toolInputs, EasyOptions options, CurlUrl url, ICollection<CallbackReadStream> reads)
    {
        if (toolInputs is not null || !PostsWithoutFields(options))
        {
            return null;
        }

        long size = options.Http.PostFieldSize;
        if (options.Callbacks.Read is not { } read)
        {
            return new StreamBody(Stream.Null, size == 0 ? 0 : null, HttpRequestOptionsMapping.FormUrlEncoded);
        }

        CallbackReadStream content = CallbackUploadOf(read, options.Callbacks, PauseGateFor(url));
        reads.Add(content);
        return new StreamBody(content, ReadCallbackPostLengthOf(options.Http), HttpRequestOptionsMapping.FormUrlEncoded);
    }

    /// <summary>
    /// The <c>Content-Length</c> of a read callback's post: <c>CURLOPT_POSTFIELDSIZE</c>, or none (chunked) when it
    /// is negative or a <c>CURLOPT_HTTPHEADER</c> line asks for <c>Transfer-Encoding: chunked</c>.
    /// </summary>
    /// <param name="http">The handle's HTTP options.</param>
    /// <returns>The length, or <see langword="null" /> for chunked.</returns>
    private static long? ReadCallbackPostLengthOf(Curl.Api.Options.Http.EasyHttpOptions http) =>
        http.PostFieldSize < 0 || AsksForChunked(http.HttpHeaders) ? null : http.PostFieldSize;

    /// <summary>
    /// Whether a <c>CURLOPT_HTTPHEADER</c> line names <c>Transfer-Encoding</c> with <c>chunked</c> in its value: then
    /// libcurl 8.22.0 ignores <c>CURLOPT_POSTFIELDSIZE</c> and sends a read callback's post as chunks (upstream
    /// test1901, BL-2667).
    /// </summary>
    /// <param name="headers">The handle's <c>CURLOPT_HTTPHEADER</c> lines.</param>
    /// <returns><see langword="true" /> when one does.</returns>
    internal static bool AsksForChunked(IReadOnlyList<string> headers) =>
        headers.Any(line => line.Split(':', 2) is [var name, var value]
            && name.Trim().Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
            && value.Contains("chunked", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The <c>CURLOPT_POSTFIELDS</c> body of a handle that sets <c>CURLOPT_TRAILERFUNCTION</c>: a
    /// <see cref="PostFieldsTrailerStream" /> of the fields' length, so a post sent chunked carries the callback's
    /// trailers in its closing chunk (upstream test1598, BL-2665). None when the curl tool's runner gave
    /// <paramref name="toolInputs" />, with no trailer callback or no fields, or for a <c>CURLOPT_UPLOAD</c>; the
    /// fields then go as <see cref="HttpRequestOptionsMapping" />'s <see cref="BytesBody" />. Only for an <c>http</c> or <c>https</c> URL.
    /// </summary>
    /// <param name="toolInputs">The runner's chosen pieces, or <see langword="null" /> for a handle's own.</param>
    /// <param name="options">The handle's options.</param>
    /// <param name="url">The URL set; only an <c>http</c> or <c>https</c> one gets the body.</param>
    /// <returns>The body, or <see langword="null" /> for none.</returns>
    internal static StreamBody? TrailerPostFieldsBodyOf(EngineToolTransferInputs? toolInputs, EasyOptions options, CurlUrl url) =>
        IsHandlesOwnHttpPost(toolInputs, options, url) && options.Callbacks.Trailer is { } trailer && options.Http.PostFields is { } fields
            ? new StreamBody(new PostFieldsTrailerStream(SizedFieldsOf(fields, options.Http.PostFieldSize), trailer), SizedFieldsOf(fields, options.Http.PostFieldSize).Length, HttpRequestOptionsMapping.FormUrlEncoded)
            : null;

    /// <summary>
    /// <paramref name="fields" /> cut to <paramref name="size" /> bytes when that is not negative and shorter, as a
    /// plain post is (<c>CURLOPT_POSTFIELDSIZE</c>, BL-3201, ADR-0773); otherwise all of them.
    /// </summary>
    /// <param name="fields">The <c>CURLOPT_POSTFIELDS</c> data.</param>
    /// <param name="size">The <c>CURLOPT_POSTFIELDSIZE</c>, or a negative number for all of the data.</param>
    /// <returns>A copy of the bytes to send.</returns>
    private static byte[] SizedFieldsOf(ReadOnlyMemory<byte> fields, long size) =>
        (size < 0 || size >= fields.Length ? fields : fields[..(int)size]).ToArray();

    /// <summary>Whether a perform with no runner's pieces posts, not uploads, to an <c>http</c> or <c>https</c> URL.</summary>
    /// <param name="toolInputs">The runner's chosen pieces, or <see langword="null" /> for a handle's own.</param>
    /// <param name="options">The handle's options.</param>
    /// <param name="url">The URL set.</param>
    /// <returns><see langword="true" /> when it does.</returns>
    private static bool IsHandlesOwnHttpPost(EngineToolTransferInputs? toolInputs, EasyOptions options, CurlUrl url) =>
        toolInputs is null && !options.Transfer.Upload && (IsScheme(url, "http") || IsScheme(url, "https"));

    /// <summary>Whether the options post with <c>CURLOPT_POST</c>, no <c>CURLOPT_POSTFIELDS</c> and no <c>CURLOPT_UPLOAD</c>.</summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>Whether the post's body is the read callback's.</returns>
    private static bool PostsWithoutFields(EasyOptions options) =>
        options.Http.Post && options.Http.PostFields is null && !options.Transfer.Upload;

    /// <summary><see cref="PerformParsedAsync" />, then disposes <paramref name="formBody" />'s content.</summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="settings">The tool settings.</param>
    /// <param name="url">The URL set, parsed.</param>
    /// <param name="urlText">The URL set.</param>
    /// <param name="transferId">The transfer's <c>%{xfer_id}</c>.</param>
    /// <param name="toolInputs">The runner's chosen pieces, or <see langword="null" /> for a handle's own.</param>
    /// <param name="formBody">The <c>CURLOPT_MIMEPOST</c> body, or <see langword="null" /> for none.</param>
    /// <param name="mimeReads">The streams of the body's <c>curl_mime_data_cb</c> parts.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The transfer's code.</returns>
    private async Task<CurlCode> PerformDisposingFormAsync(
        EasyOptions options,
        EngineTransferSettings settings,
        CurlUrl url,
        string urlText,
        long transferId,
        EngineToolTransferInputs? toolInputs,
        StreamBody? formBody,
        IReadOnlyList<CallbackReadStream> mimeReads,
        CancellationToken cancellationToken)
    {
        await using Stream? formContent = formBody?.Content;
        return await PerformParsedAsync(options, settings, url, urlText, transferId, toolInputs, formBody, mimeReads, cancellationToken).ConfigureAwait(false);
    }
}
