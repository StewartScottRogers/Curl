namespace Curl.Protocol.Http;

/// <summary>
/// A response's Transfer-Encoding headers as <c>--tr-encoding</c> reads them
/// (<see cref="HttpTransferEncoding.Requested" />): whether the body is chunked, where the
/// first Transfer-Encoding header is, and the codings to decode after the chunked coding.
/// </summary>
/// <param name="IsChunked">Whether the body is decoded as chunked transfer coding.</param>
/// <param name="FirstHeaderIndex">
/// The index of the first Transfer-Encoding header among the response's headers, or
/// <see langword="null" /> when there is none.
/// </param>
/// <param name="Codings">
/// Every coding other than <c>chunked</c>, in the order the headers list them, which is the
/// order the server applied them; <c>identity</c> included.
/// </param>
internal readonly record struct HttpTransferCodings(bool IsChunked, int? FirstHeaderIndex, IReadOnlyList<string> Codings);
