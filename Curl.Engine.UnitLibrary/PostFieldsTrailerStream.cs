using Curl.Api.Callbacks;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// A read-only, seekable stream over a handle's <c>CURLOPT_POSTFIELDS</c> bytes that carries its
/// <c>CURLOPT_TRAILERFUNCTION</c> (<see cref="TrailerCallback" />), so a post made chunked by a
/// <c>Transfer-Encoding: chunked</c> header sends the lines the callback adds in its closing chunk,
/// as curl 8.22.0 does for any chunked body (upstream test1598, BL-2665). The HTTP handler asks it,
/// through <see cref="IUploadTrailerSource" />, once the body's end is read; a body that is not sent
/// chunked never asks.
/// </summary>
/// <param name="postFields">The post's bytes.</param>
/// <param name="trailer">The handle's trailer callback.</param>
internal sealed class PostFieldsTrailerStream(byte[] postFields, TrailerCallback trailer)
    : MemoryStream(postFields, writable: false), IUploadTrailerSource
{
    /// <inheritdoc />
    public bool SuppliesTrailers => true;

    /// <inheritdoc />
    public bool TryTakeTrailers(out IReadOnlyList<string> lines)
    {
        List<string> trailers = [];
        lines = trailers;
        return trailer(trailers) == TrailerResult.Ok;
    }
}
