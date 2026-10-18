using Curl.Api.Callbacks;
using Curl.Api.Options.Http;
using Curl.Core.Multipart;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The body a perform from the options sends for <see cref="EasyHttpOptions.MimePost" /> (<c>CURLOPT_MIMEPOST</c>)
/// or, with none, <see cref="EasyHttpOptions.HttpPost" /> (<c>CURLOPT_HTTPPOST</c>),
/// built as the curl tool builds its <c>-F</c> body (<c>CurlCommandRunner.BuildFormAsync</c>): the parts mapped by
/// <see cref="MultipartFormPartMapping" />, names escaped under <see cref="MimeOptions.FormEscape" /> or for a mail
/// URL, and, to an <c>smtp</c>, <c>smtps</c>, <c>imap</c> or <c>imaps</c> URL, the MIME mail message headed by
/// <see cref="EasyHttpOptions.HttpHeaders" /> and uploaded, else the form body posted.
/// </summary>
internal static class MimePostForm
{
    /// <summary>Builds the body of <paramref name="http" />'s <see cref="BodyOf">MIME or form post</see>.</summary>
    /// <param name="builder">Builds the body.</param>
    /// <param name="http">The handle's HTTP options.</param>
    /// <param name="scheme">The transfer URL's scheme.</param>
    /// <param name="read">The handle's <c>CURLOPT_READFUNCTION</c>, which reads a <c>CURLFORM_STREAM</c> part.</param>
    /// <param name="callbackReads">
    /// Receives the stream of each <c>curl_mime_data_cb</c> part as the body is built
    /// (<see cref="MultipartFormPartMapping.FromMimeBody" />); <see langword="null" /> keeps none.
    /// </param>
    /// <param name="pauseGate">The handle's pause state a callback part's <c>CURL_READFUNC_PAUSE</c> waits on (BL-2631), or <see langword="null" /> when the transfer cannot pause.</param>
    /// <param name="standardInput">What a <c>CURLOPT_HTTPPOST</c> file part named <c>-</c> reads (<see cref="BodyOf" />).</param>
    /// <param name="cancellationToken">Cancels the file opens.</param>
    /// <returns>
    /// The body or its failure, or <see langword="null" /> when <see cref="BodyOf" /> gives none; an empty
    /// <see cref="EasyHttpOptions.HttpPost" /> form is an empty body with no <c>Content-Type</c>, which curl 8.22.0
    /// posts with <c>Content-Length: 0</c> (upstream test516).
    /// </returns>
    internal static async ValueTask<MultipartFormBuildResult?> BuildAsync(
        MultipartFormBodyBuilder builder,
        EasyHttpOptions http,
        string scheme,
        ReadCallback? read,
        ICollection<CallbackReadStream>? callbackReads,
        EasyPauseGate? pauseGate,
        Stream standardInput,
        CancellationToken cancellationToken)
    {
        if (BodyOf(http, scheme, read, standardInput) is not { } body)
        {
            return null;
        }

        if (http.MimePost is null && body.Parts.Count == 0)
        {
            return new MultipartFormBuildResult(new StreamBody(new MemoryStream([], writable: false), 0, string.Empty), null);
        }

        IReadOnlyList<MultipartFormPart> parts = MultipartFormPartMapping.FromMimeBody(body, callbackReads, pauseGate);
        MultipartNameEscaping escaping = MultipartFormPartMapping.NameEscapingOf(http.MimeOptions.HasFlag(MimeOptions.FormEscape), scheme);
        return MailRequestOptionsMapping.SendsFormAsMimeMessage(scheme)
            ? await builder.BuildMailMessageAsync(parts, escaping, http.HttpHeaders, cancellationToken).ConfigureAwait(false)
            : await builder.BuildAsync(parts, escaping, MultipartFormPartMapping.RequestContentTypeOf(http.HttpHeaders), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The body to send: <see cref="EasyHttpOptions.MimePost" />, else, to a URL that is not a mail one (curl 8.22.0
    /// sends <c>CURLOPT_HTTPPOST</c> over HTTP only), <see cref="EasyHttpOptions.HttpPost" /> with each
    /// <c>CURLFORM_STREAM</c> part read through <paramref name="read" /> (<see cref="FormStreamData.WithReadCallback" />)
    /// and each file part named <c>-</c> read from <paramref name="standardInput" /> at an unknown size, as curl 8.22.0's
    /// <c>Curl_getformdata</c> hands <c>stdin</c> to <c>curl_mime_data_cb</c> with <c>fread</c> and <c>fseeko</c>, so the
    /// body is sent chunked (upstream test650, BL-2625).
    /// </summary>
    /// <param name="http">The handle's HTTP options.</param>
    /// <param name="scheme">The transfer URL's scheme.</param>
    /// <param name="read">The handle's <c>CURLOPT_READFUNCTION</c>, or <see langword="null" /> when unset.</param>
    /// <param name="standardInput">What a file part named <c>-</c> reads; <see langword="null" /> leaves such a part a file.</param>
    /// <returns>The body, or <see langword="null" /> for none.</returns>
    internal static EasyMimeBody? BodyOf(EasyHttpOptions http, string scheme, ReadCallback? read, Stream? standardInput = null) =>
        http.MimePost ?? (http.HttpPost is { } form && !MailRequestOptionsMapping.SendsFormAsMimeMessage(scheme) ? FormStreamData.WithReadCallback(WithStandardInput(form, standardInput), read) : null);

    /// <summary><paramref name="form" /> with each part, or file of a field's several files, named <c>-</c> read from <paramref name="standardInput" />.</summary>
    private static EasyMimeBody WithStandardInput(EasyMimeBody form, Stream? standardInput) =>
        standardInput is null ? form : new() { Parts = [.. form.Parts.Select(part => WithStandardInput(part, standardInput))] };

    private static EasyMimePart WithStandardInput(EasyMimePart part, Stream standardInput) =>
        part switch
        {
            { Data: EasyMimeStandardInput } => part with { Data = new EasyMimeCallback(-1, buffer => standardInput.Read(buffer), (offset, origin) => Seek(standardInput, offset, origin)) },
            { SubParts: { } files } => part with { SubParts = WithStandardInput(files, standardInput) },
            _ => part,
        };

    /// <summary><c>fseeko</c> on standard input: <see cref="SeekResult.CantSeek" /> when it cannot seek.</summary>
    private static SeekResult Seek(Stream standardInput, long offset, SeekOrigin origin)
    {
        if (!standardInput.CanSeek)
        {
            return SeekResult.CantSeek;
        }

        standardInput.Seek(offset, origin);
        return SeekResult.Ok;
    }

    /// <summary>
    /// The transfer's inputs carrying <paramref name="body" />: as the upload of a mail URL, as the runner hands a
    /// mail message over, else as the form body.
    /// </summary>
    /// <param name="inputs">The inputs the handle chose.</param>
    /// <param name="body">The built body, or <see langword="null" /> when no <c>CURLOPT_MIMEPOST</c> is set.</param>
    /// <param name="scheme">The transfer URL's scheme.</param>
    /// <returns>The inputs.</returns>
    internal static EngineToolTransferInputs WithBody(EngineToolTransferInputs inputs, StreamBody? body, string scheme) =>
        body is null ? inputs
        : MailRequestOptionsMapping.SendsFormAsMimeMessage(scheme) ? inputs with { Upload = body.Content }
        : inputs with { FormBody = body };
}
