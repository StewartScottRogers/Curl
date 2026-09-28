using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Decodes a response body's Content-Encoding for <c>--compressed</c> and writes the decoded
/// bytes to the output, as curl 8.21.0 does (measured, BL-177 Notes).
/// </summary>
/// <remarks>
/// Every Content-Encoding header's comma-separated codings are read in order, compared
/// without regard to case, with blanks around each and empty items ignored; <c>identity</c>
/// is skipped, <c>gzip</c>, <c>x-gzip</c>, <c>deflate</c> and <c>br</c> are decoded, last
/// applied first, and any other coding is exit 61
/// <see cref="HttpTransferMessages.UnrecognizedContentEncoding" /> once the first body byte
/// arrives: an empty body with an unrecognized coding is no error, as measured. For
/// <c>--tr-encoding</c> the Transfer-Encoding codings other than <c>chunked</c> are decoded the
/// same way, before the Content-Encoding ones, with the same messages (measured, BL-315 Notes).
/// More than <see cref="MaximumCodings" /> Content-Encoding codings are refused while the
/// headers are read (<see cref="HttpResponseBodyReader.FindHeadRefusal" />).
/// </remarks>
internal sealed class HttpContentDecoder : IDisposable
{
    /// <summary>
    /// The most codings <c>--compressed</c> accepts across a response's Content-Encoding
    /// headers: curl 8.21.0's limit, counted apart from the Transfer-Encoding one (measured,
    /// BL-364 Notes).
    /// </summary>
    internal const int MaximumCodings = 5;

    private const string HeaderName = "Content-Encoding";

    private static readonly char[] Blanks = [' ', '\t'];

    private readonly HttpContentCodingDecoder[] layers;

    private readonly bool hasUnrecognizedCoding;

    private HttpContentDecoder(HttpContentCodingDecoder[] layers, bool hasUnrecognizedCoding)
    {
        this.layers = layers;
        this.hasUnrecognizedCoding = hasUnrecognizedCoding;
    }

    /// <summary>
    /// Builds the decoder the response's Content-Encoding headers call for.
    /// </summary>
    /// <param name="headers">The final response's headers.</param>
    /// <returns>The decoder, or <see langword="null" /> when the body has no coding to decode.</returns>
    internal static HttpContentDecoder? For(IReadOnlyList<HttpResponseHeader> headers) => ForCodings(ContentCodings(headers));

    /// <summary>
    /// Builds the decoder for <paramref name="codings" />, in the order the server applied
    /// them: the Content-Encoding codings, then, for <c>--tr-encoding</c>, the Transfer-Encoding
    /// codings other than <c>chunked</c> (BL-315 Notes). <c>identity</c> is skipped, and the
    /// last applied is decoded first.
    /// </summary>
    /// <param name="codings">The codings, each without blanks.</param>
    /// <returns>The decoder, or <see langword="null" /> when there is no coding to decode.</returns>
    internal static HttpContentDecoder? ForCodings(IEnumerable<string> codings) =>
        Of([.. codings.Where(coding => !Is(coding, "identity")).Select(CodingOf)]);

    /// <summary>
    /// Lists every Content-Encoding header's codings in order, without blanks or empty items.
    /// </summary>
    /// <param name="headers">The final response's headers.</param>
    /// <returns>The codings.</returns>
    internal static IEnumerable<string> ContentCodings(IReadOnlyList<HttpResponseHeader> headers) =>
        headers
            .Where(IsContentEncoding)
            .SelectMany(header => CodingsOf(header.Value));

    /// <summary>
    /// Finds the Content-Encoding header whose codings take the response past
    /// <see cref="MaximumCodings" />, counting every coding of every header, <c>identity</c> and
    /// unrecognized ones included, as curl 8.21.0 does (measured, BL-364 Notes).
    /// </summary>
    /// <param name="headers">The response's headers.</param>
    /// <returns>The header's index, or <see langword="null" /> when the codings are within the limit.</returns>
    internal static int? IndexPastCodingLimit(IReadOnlyList<HttpResponseHeader> headers)
    {
        int count = 0;
        for (int index = 0; index < headers.Count; index++)
        {
            count += IsContentEncoding(headers[index]) ? CodingsOf(headers[index].Value).Count() : 0;
            if (count > MaximumCodings)
            {
                return index;
            }
        }

        return null;
    }

    private static bool IsContentEncoding(HttpResponseHeader header) =>
        string.Equals(header.Name, HeaderName, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> CodingsOf(string value) =>
        value.Split(',').Select(item => item.Trim(Blanks)).Where(item => item.Length > 0);

    private static HttpContentDecoder? Of(HttpContentCoding?[] codings)
    {
        if (codings.Length == 0)
        {
            return null;
        }

        return codings.Contains(null)
            ? new HttpContentDecoder([], hasUnrecognizedCoding: true)
            : new HttpContentDecoder(
                [.. codings.Reverse().Select(coding => new HttpContentCodingDecoder(coding.GetValueOrDefault()))],
                hasUnrecognizedCoding: false);
    }

    /// <summary>
    /// Decodes the next encoded bytes and writes what they decode to, each piece as one write.
    /// </summary>
    /// <param name="output">Where the decoded body goes.</param>
    /// <param name="encoded">The next encoded body bytes; never empty.</param>
    /// <param name="cancellationToken">Cancels every write.</param>
    /// <returns>A task that completes when the decoded bytes are written.</returns>
    /// <exception cref="HttpTransferException">
    /// A coding is not one curl decodes, or the encoded bytes are corrupt (exit 61).
    /// </exception>
    internal ValueTask WriteAsync(Stream output, ReadOnlyMemory<byte> encoded, CancellationToken cancellationToken) =>
        hasUnrecognizedCoding
            ? throw new HttpTransferException(CurlExitCode.BadContentEncoding, HttpTransferMessages.UnrecognizedContentEncoding)
            : WriteThroughAsync(0, encoded, output, cancellationToken);

    /// <summary>
    /// Releases every coding's decompression stream.
    /// </summary>
    public void Dispose()
    {
        foreach (HttpContentCodingDecoder layer in layers)
        {
            layer.Dispose();
        }
    }

    private async ValueTask WriteThroughAsync(int layer, ReadOnlyMemory<byte> bytes, Stream output, CancellationToken cancellationToken)
    {
        if (layer == layers.Length)
        {
            await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            return;
        }

        foreach (ReadOnlyMemory<byte> decoded in layers[layer].Decode(bytes))
        {
            await WriteThroughAsync(layer + 1, decoded, output, cancellationToken).ConfigureAwait(false);
        }
    }

    private static HttpContentCoding? CodingOf(string coding)
    {
        if (Is(coding, "gzip") || Is(coding, "x-gzip"))
        {
            return HttpContentCoding.Gzip;
        }

        if (Is(coding, "deflate"))
        {
            return HttpContentCoding.Deflate;
        }

        return Is(coding, "br") ? HttpContentCoding.Brotli : null;
    }

    private static bool Is(string coding, string name) =>
        string.Equals(coding, name, StringComparison.OrdinalIgnoreCase);
}
