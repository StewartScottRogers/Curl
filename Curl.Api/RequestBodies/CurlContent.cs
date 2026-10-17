using Curl.Api.EngineContracts;
using Curl.Api.Options;

namespace Curl.Api;

/// <summary>
/// The body of a <see cref="CurlRequest"/>: in-memory bytes, sent as <c>CURLOPT_POSTFIELDS</c>, or a
/// <see cref="Stream"/>, uploaded through the read callback and rewound through the seek callback
/// (ADR-0634), or a <see cref="CurlMultipartContent"/>, sent as <c>CURLOPT_MIMEPOST</c> (ADR-0637). A byte array or <see cref="ReadOnlyMemory{T}"/> converts to bytes content implicitly.
/// </summary>
public abstract class CurlContent
{
    private protected CurlContent()
    {
    }

    /// <summary>Content of no bytes.</summary>
    public static CurlContent Empty { get; } = new BytesContent(ReadOnlyMemory<byte>.Empty);

    /// <summary>Content of <paramref name="bytes"/>, sent as given (<c>CURLOPT_POSTFIELDS</c> with its size).</summary>
    /// <param name="bytes">The body; the caller does not change it while a request uses it.</param>
    /// <returns>The content.</returns>
    public static CurlContent FromBytes(ReadOnlyMemory<byte> bytes) => new BytesContent(bytes);

    /// <summary>
    /// Content read from <paramref name="stream"/>, from its position when the request is sent, through
    /// <c>CURLOPT_READFUNCTION</c>. A seekable stream is rewound to that position when the engine asks
    /// (<c>CURLOPT_SEEKFUNCTION</c>), as a redirect or an authentication retry does; a non-seekable one
    /// answers <see cref="Callbacks.SeekResult.CantSeek"/>. A read or seek that throws fails the transfer
    /// with <see cref="Codes.CurlCode.AbortedByCallback"/>. The stream is not disposed.
    /// </summary>
    /// <param name="stream">The readable stream to upload.</param>
    /// <param name="length">
    /// The number of bytes to send (<c>CURLOPT_POSTFIELDSIZE_LARGE</c>), or <see langword="null"/> for
    /// the seekable stream's remaining length, and for a non-seekable one an unknown length (-1), which
    /// the engine sends chunked over HTTP/1.1.
    /// </param>
    /// <returns>The content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="stream"/> cannot be read.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    public static CurlContent FromStream(Stream stream, long? length = null)
    {
        ThrowIfNotUploadable(stream, length);
        return new StreamContent(stream, length);
    }

    /// <summary>Throws unless <paramref name="stream"/> can be uploaded with <paramref name="length"/>.</summary>
    /// <param name="stream">The stream to upload.</param>
    /// <param name="length">The bytes to send, or <see langword="null"/> to work it out when sent.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="stream"/> cannot be read.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    internal static void ThrowIfNotUploadable(Stream stream, long? length)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream cannot be read.", nameof(stream));
        }

        if (length is { } known)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(known, nameof(length));
        }
    }

    /// <summary>Bytes content of <paramref name="bytes"/>; the same as <see cref="FromBytes"/>.</summary>
    /// <param name="bytes">The body.</param>
    public static implicit operator CurlContent(ReadOnlyMemory<byte> bytes) => FromBytes(bytes);

    /// <summary>Bytes content of <paramref name="bytes"/>; the same as <see cref="FromBytes"/>.</summary>
    /// <param name="bytes">The body.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is <see langword="null"/>.</exception>
    public static implicit operator CurlContent(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return FromBytes(bytes);
    }

    /// <summary>
    /// <paramref name="options"/> with this content set as the body of a posting request: the post
    /// fields, the read and seek callbacks, or the <c>CURLOPT_MIMEPOST</c> body, and the size.
    /// </summary>
    /// <param name="options">The options, with <c>CURLOPT_POST</c> already set.</param>
    /// <param name="engine">The engine performing the request, whose handles a body may create.</param>
    /// <param name="owned">Takes every handle the body creates, to dispose when the request ends.</param>
    /// <returns>The options to perform the request with.</returns>
    internal abstract EasyOptions ApplyTo(EasyOptions options, ICurlEngine engine, ICollection<IDisposable> owned);
}
