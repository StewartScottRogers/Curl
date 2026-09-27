using System.Buffers;

namespace Curl.Core.Multipart;

/// <summary>
/// One of the five transfer encoders a <c>-F</c> part can name with <c>;encoder=</c>, as
/// libcurl 8.21.0's <c>encoders[]</c> in <c>lib/mime.c</c> defines them: <c>binary</c> and
/// <c>8bit</c> send the data as it is, <c>7bit</c> sends it as it is but refuses a byte above
/// 127, and <c>base64</c> and <c>quoted-printable</c> encode it in 76-column CRLF lines.
/// </summary>
internal sealed class MultipartPartEncoder
{
    private static readonly MultipartPartEncoder[] Encoders =
    [
        new("binary", () => new PassThroughDataEncoding(refusesEightBitData: false), dataLength => dataLength),
        new("8bit", () => new PassThroughDataEncoding(refusesEightBitData: false), dataLength => dataLength),
        new("7bit", () => new PassThroughDataEncoding(refusesEightBitData: true), dataLength => dataLength),
        new("base64", () => new Base64DataEncoding(), dataLength => Base64DataEncoding.EncodedLength(dataLength)),
        new("quoted-printable", () => new QuotedPrintableDataEncoding(), dataLength => dataLength == 0 ? 0 : null),
    ];

    private readonly Func<MultipartDataEncoding> createEncoding;

    private readonly Func<long, long?> encodedLength;

    private MultipartPartEncoder(string name, Func<MultipartDataEncoding> createEncoding, Func<long, long?> encodedLength)
    {
        Name = name;
        this.createEncoding = createEncoding;
        this.encodedLength = encodedLength;
    }

    /// <summary>Gets the encoder's name as curl sends it in <c>Content-Transfer-Encoding</c>: lower case.</summary>
    internal string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the encoder sends every byte as it is and refuses none,
    /// so a file part using it is sent exactly as one with no encoder.
    /// </summary>
    internal bool PassesDataThrough => Name is "binary" or "8bit";

    /// <summary>
    /// Gets a value indicating whether the encoder can refuse data, as <c>7bit</c> refuses a byte
    /// above 127, so a file part using it is checked before it is sent.
    /// </summary>
    internal bool CanRefuseData => Name == "7bit";

    /// <summary>Finds the encoder named <paramref name="name" />, compared without regard to case as curl compares.</summary>
    /// <param name="name">The <c>;encoder=</c> value.</param>
    /// <returns>The encoder, or <see langword="null" /> when curl has none by that name.</returns>
    internal static MultipartPartEncoder? Find(string name) =>
        Array.Find(Encoders, encoder => encoder.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Encodes <paramref name="data" /> whole.</summary>
    /// <param name="data">The part's data.</param>
    /// <returns>The encoded bytes, or <see langword="null" /> when the encoder cannot carry the data.</returns>
    internal byte[]? Encode(byte[] data)
    {
        ArrayBufferWriter<byte> output = new();
        return createEncoding().TryEncode(data, isFinal: true, output, out _) ? output.WrittenSpan.ToArray() : null;
    }

    /// <summary>Encodes <paramref name="data" /> as it is read, never holding it whole.</summary>
    /// <param name="data">The part's data, which the returned stream now owns.</param>
    /// <param name="dataLength">The data's size in bytes, or <see langword="null" /> when unknown.</param>
    /// <returns>The encoded data, whose reads throw <see cref="MultipartDataRefusedException" /> at data the encoder refuses.</returns>
    internal EncodedReadStream EncodeWhileReading(Stream data, long? dataLength) =>
        new(data, createEncoding, EncodedLength(dataLength));

    /// <summary>
    /// Gives the encoded size curl knows before it sends: the data's own size for the encoders
    /// that send it as it is, the size of its 76-column lines for <c>base64</c>, and nothing for
    /// <c>quoted-printable</c>, whose size curl learns only by encoding, unless the data is empty.
    /// </summary>
    /// <param name="dataLength">The data's size in bytes, or <see langword="null" /> when unknown.</param>
    /// <returns>The encoded size in bytes, or <see langword="null" /> when curl does not know it beforehand.</returns>
    internal long? EncodedLength(long? dataLength) => dataLength is { } length ? encodedLength(length) : null;
}
