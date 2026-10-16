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
    /// <param name="cancellationToken">Cancels the file opens.</param>
    /// <returns>The body or its failure, or <see langword="null" /> when <see cref="BodyOf" /> gives none.</returns>
    internal static async ValueTask<MultipartFormBuildResult?> BuildAsync(
        MultipartFormBodyBuilder builder,
        EasyHttpOptions http,
        string scheme,
        ReadCallback? read,
        ICollection<CallbackReadStream>? callbackReads,
        CancellationToken cancellationToken)
    {
        if (BodyOf(http, scheme, read) is not { } body)
        {
            return null;
        }

        IReadOnlyList<MultipartFormPart> parts = MultipartFormPartMapping.FromMimeBody(body, callbackReads);
        MultipartNameEscaping escaping = MultipartFormPartMapping.NameEscapingOf(http.MimeOptions.HasFlag(MimeOptions.FormEscape), scheme);
        return MailRequestOptionsMapping.SendsFormAsMimeMessage(scheme)
            ? await builder.BuildMailMessageAsync(parts, escaping, http.HttpHeaders, cancellationToken).ConfigureAwait(false)
            : await builder.BuildAsync(parts, escaping, MultipartFormPartMapping.RequestContentTypeOf(http.HttpHeaders), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The body to send: <see cref="EasyHttpOptions.MimePost" />, else, to a URL that is not a mail one (curl 8.22.0
    /// sends <c>CURLOPT_HTTPPOST</c> over HTTP only), <see cref="EasyHttpOptions.HttpPost" /> with each
    /// <c>CURLFORM_STREAM</c> part read through <paramref name="read" /> (<see cref="FormStreamData.WithReadCallback" />).
    /// </summary>
    /// <param name="http">The handle's HTTP options.</param>
    /// <param name="scheme">The transfer URL's scheme.</param>
    /// <param name="read">The handle's <c>CURLOPT_READFUNCTION</c>, or <see langword="null" /> when unset.</param>
    /// <returns>The body, or <see langword="null" /> for none.</returns>
    internal static EasyMimeBody? BodyOf(EasyHttpOptions http, string scheme, ReadCallback? read) =>
        http.MimePost ?? (http.HttpPost is { } form && !MailRequestOptionsMapping.SendsFormAsMimeMessage(scheme) ? FormStreamData.WithReadCallback(form, read) : null);

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
