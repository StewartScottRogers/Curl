using System.Text;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Errors;

namespace Curl.Api;

/// <summary>
/// One part of a <see cref="CurlMultipartContent"/> (<c>curl_mimepart</c>): its content - text, bytes,
/// a file, a stream or a nested multipart body, chosen by the factory that made it - and the field
/// name, file name, content type, transfer encoder and extra headers. Immutable; change one property
/// with a <see langword="with"/> expression.
/// </summary>
public sealed record CurlMimePart
{
    private CurlMimePart(string? name, MimeDataSetter setData)
    {
        Name = name;
        SetData = setData;
    }

    /// <summary>The form field name (<c>curl_mime_name</c>), or <see langword="null"/> for none.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// The file name sent in <c>Content-Disposition</c> (<c>curl_mime_filename</c>). <see langword="null"/>
    /// sends none, except that a <see cref="File"/> part then sends its file's base name.
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>The <c>Content-Type</c> (<c>curl_mime_type</c>), or <see langword="null"/> for the engine's guess.</summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// The transfer encoder (<c>curl_mime_encoder</c>): <c>binary</c>, <c>8bit</c>, <c>7bit</c>, <c>base64</c>
    /// or <c>quoted-printable</c>, or <see langword="null"/> for none. Any other name fails the send with a
    /// <see cref="CurlException"/> of <see cref="Codes.CurlCode.BadFunctionArgument"/>.
    /// </summary>
    public string? Encoder { get; init; }

    /// <summary>The part's extra headers (<c>curl_mime_headers</c>), sent as <c>Name: value</c> in this order. Default: none.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Headers
    {
        get;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = [];

    /// <summary>Sets the part's content on the engine's part.</summary>
    private MimeDataSetter SetData { get; }

    /// <summary>A part of <paramref name="value"/>, sent in UTF-8 (<c>curl_mime_data</c>).</summary>
    /// <param name="name">The field name, or <see langword="null"/> for none.</param>
    /// <param name="value">The text.</param>
    /// <returns>The part.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static CurlMimePart Text(string? name, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Bytes(name, Encoding.UTF8.GetBytes(value));
    }

    /// <summary>A part of <paramref name="bytes"/>, copied when the request is sent (<c>curl_mime_data</c>).</summary>
    /// <param name="name">The field name, or <see langword="null"/> for none.</param>
    /// <param name="bytes">The content.</param>
    /// <returns>The part.</returns>
    public static CurlMimePart Bytes(string? name, ReadOnlyMemory<byte> bytes) =>
        new(name, (part, _, _) => part.SetData(bytes.ToArray()));

    /// <summary>
    /// A part read from the file at <paramref name="path"/> (<c>curl_mime_filedata</c>), whose base name
    /// is the file name unless <see cref="FileName"/> says otherwise. A file the engine cannot read fails
    /// the send with a <see cref="CurlException"/> of <see cref="Codes.CurlCode.ReadError"/>.
    /// </summary>
    /// <param name="name">The field name, or <see langword="null"/> for none.</param>
    /// <param name="path">The file's path.</param>
    /// <returns>The part.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    public static CurlMimePart File(string? name, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return new(name, (part, _, _) => part.SetFileData(path));
    }

    /// <summary>
    /// A part read from <paramref name="stream"/> through a read callback (<c>curl_mime_data_cb</c>), from
    /// its position when the request is sent, rewound there when the engine asks, as for
    /// <see cref="CurlContent.FromStream"/>. The stream is not disposed.
    /// </summary>
    /// <param name="name">The field name, or <see langword="null"/> for none.</param>
    /// <param name="stream">The readable stream.</param>
    /// <param name="length">The bytes to send, or <see langword="null"/> for the seekable stream's remaining length, and -1 (unknown) for a non-seekable one.</param>
    /// <returns>The part.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="stream"/> cannot be read.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    public static CurlMimePart Stream(string? name, System.IO.Stream stream, long? length = null)
    {
        CurlContent.ThrowIfNotUploadable(stream, length);
        return new(name, (part, _, _) =>
        {
            (StreamUpload upload, long size) = StreamUpload.FromPosition(stream, length);
            return part.SetDataCallback(size, upload.Read, upload.Seek);
        });
    }

    /// <summary>A part whose content is the nested multipart body <paramref name="parts"/> (<c>curl_mime_subparts</c>).</summary>
    /// <param name="name">The field name, or <see langword="null"/> for none.</param>
    /// <param name="parts">The nested body.</param>
    /// <returns>The part.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parts"/> is <see langword="null"/>.</exception>
    public static CurlMimePart Multipart(string? name, CurlMultipartContent parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return new(name, (part, engine, owned) => part.SetSubParts(parts.BuildOn(engine, owned)));
    }

    /// <summary>Fills the engine's <paramref name="part"/> with this part, throwing at the first setter that fails.</summary>
    /// <param name="part">The engine's part.</param>
    /// <param name="engine">The engine performing the request.</param>
    /// <param name="owned">Takes every handle created, to dispose when the request ends.</param>
    /// <exception cref="CurlException">A setter did not return <see cref="Codes.CurlCode.Ok"/>.</exception>
    internal void SetOn(IMimePartBuilder part, ICurlEngine engine, ICollection<IDisposable> owned)
    {
        CurlException.ThrowIfFailed(part.SetName(Name), string.Empty);
        CurlException.ThrowIfFailed(SetData(part, engine, owned), string.Empty);
        if (FileName is not null)
        {
            CurlException.ThrowIfFailed(part.SetFileName(FileName), string.Empty);
        }

        CurlException.ThrowIfFailed(part.SetType(ContentType), string.Empty);
        CurlException.ThrowIfFailed(part.SetEncoder(Encoder), string.Empty);
        CurlException.ThrowIfFailed(part.SetHeaders([.. Headers.Select(HeaderLine)]), string.Empty);
    }

    /// <summary>The <c>curl_slist</c> line of one part header: <c>Name: value</c>.</summary>
    private static string HeaderLine(KeyValuePair<string, string> header) => $"{header.Key}: {header.Value}";
}
