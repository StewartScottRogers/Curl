using Curl.Api.Callbacks;
using Curl.Api.Options.Http;

namespace Curl.Engine;

/// <summary>
/// The content of a <c>CURLFORM_STREAM</c> field in a <c>CURLOPT_HTTPPOST</c> body: read when the form is sent through
/// the easy handle's <c>CURLOPT_READFUNCTION</c>, which <c>curl_formadd</c> does not know, so
/// <see cref="WithReadCallback" /> turns it into a <see cref="EasyMimeCallback" /> at perform time as curl 8.22.0's
/// <c>Curl_getformdata</c> does with <c>data-&gt;state.fread_func</c> (ADR-0651).
/// </summary>
/// <param name="ContentsLength">
/// <c>CURLFORM_CONTENTSLENGTH</c>: the content's size, 0 for unknown (sent chunked, as <c>Curl_getformdata</c> turns
/// 0 into -1).
/// </param>
/// <param name="StreamObject">
/// <c>CURLFORM_STREAM</c>'s object, which upstream hands its read callback; a managed <see cref="ReadCallback" />
/// carries its user data in its closure, so it is kept here only as given.
/// </param>
internal sealed record FormStreamData(long ContentsLength, object? StreamObject) : EasyMimeData
{
    /// <summary>
    /// <paramref name="body" /> with each top-level <see cref="FormStreamData" /> part read through
    /// <paramref name="read" />, sized by its <see cref="ContentsLength" /> (-1 for 0), or empty when no read callback is
    /// set. Only a field's own part can hold a stream: <c>curl_formadd</c> reads a field with several files as files.
    /// </summary>
    /// <param name="body">The <c>CURLOPT_HTTPPOST</c> body.</param>
    /// <param name="read">The handle's <c>CURLOPT_READFUNCTION</c>, or <see langword="null" /> when unset.</param>
    /// <returns>The body to send.</returns>
    internal static EasyMimeBody WithReadCallback(EasyMimeBody body, ReadCallback? read) =>
        new() { Parts = [.. body.Parts.Select(part => part.Data is FormStreamData stream ? part with { Data = stream.ReadThrough(read) } : part)] };

    private EasyMimeCallback? ReadThrough(ReadCallback? read) =>
        read is null ? null : new EasyMimeCallback(ContentsLength == 0 ? -1 : ContentsLength, read, null);
}
