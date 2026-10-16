using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options.Http;

namespace Curl.Engine;

/// <summary>
/// One part of a <see cref="ManagedMimeBuilder" />: the <c>curl_mime_*</c> setters as curl 8.22.0's
/// <c>lib/mime.c</c> applies them. Each data source (<see cref="SetData" />, <see cref="SetFileData" />,
/// <see cref="SetDataCallback" />, <see cref="SetSubParts" />) replaces the one set before, a nested body
/// it replaces being freed of this part; <see cref="Build" /> gives the <see cref="EasyMimePart" />.
/// </summary>
/// <param name="owner">The body the part was added to: libcurl's <c>curl_mimepart.parent</c>.</param>
internal sealed class ManagedMimePartBuilder(ManagedMimeBuilder owner) : IMimePartBuilder
{
    /// <summary>The encoder names <c>curl_mime_encoder</c> accepts, in any case.</summary>
    private static readonly string[] EncoderNames = ["binary", "8bit", "7bit", "base64", "quoted-printable"];

    private string? name;
    private string? fileName;
    private string? type;
    private string? encoder;
    private IReadOnlyList<string> headers = [];
    private EasyMimeData? data;
    private ManagedMimeBuilder? subParts;

    /// <inheritdoc />
    public CurlCode SetName(string? name)
    {
        this.name = name;
        return CurlCode.Ok;
    }

    /// <inheritdoc />
    public CurlCode SetFileName(string? fileName)
    {
        this.fileName = fileName;
        return CurlCode.Ok;
    }

    /// <inheritdoc />
    public CurlCode SetType(string? type)
    {
        this.type = type;
        return CurlCode.Ok;
    }

    /// <summary>
    /// <c>curl_mime_encoder</c>: <see langword="null" /> clears the encoder; one of <c>binary</c>, <c>8bit</c>,
    /// <c>7bit</c>, <c>base64</c> or <c>quoted-printable</c>, in any case, is kept as given; any other name
    /// leaves the part as it was.
    /// </summary>
    /// <param name="encoding">The encoder's name, or <see langword="null" />.</param>
    /// <returns><see cref="CurlCode.Ok" />, or <see cref="CurlCode.BadFunctionArgument" /> for an unknown name.</returns>
    public CurlCode SetEncoder(string? encoding)
    {
        if (encoding is not null && !EncoderNames.Contains(encoding, StringComparer.OrdinalIgnoreCase))
        {
            return CurlCode.BadFunctionArgument;
        }

        encoder = encoding;
        return CurlCode.Ok;
    }

    /// <summary><c>curl_mime_data</c>: the bytes, copied, or no data for <see langword="null" />.</summary>
    /// <param name="data">The bytes, or <see langword="null" />.</param>
    /// <returns><see cref="CurlCode.Ok" />.</returns>
    public CurlCode SetData(byte[]? data)
    {
        ReplaceData(data is null ? null : new EasyMimeBytes(data.ToArray()));
        return CurlCode.Ok;
    }

    /// <summary>
    /// <c>curl_mime_filedata</c>: the file's content, read when the transfer sends it, and its base name as the file
    /// name; <see langword="null" /> clears the data and leaves the file name. A file that does not exist is still
    /// set, so the transfer fails reading it as the tool's <c>-F name=@file</c> does, and answers
    /// <see cref="CurlCode.ReadError" />.
    /// </summary>
    /// <param name="path">The file's path, or <see langword="null" />.</param>
    /// <returns><see cref="CurlCode.Ok" />, or <see cref="CurlCode.ReadError" /> when the file does not exist.</returns>
    public CurlCode SetFileData(string? path)
    {
        if (path is null)
        {
            ReplaceData(null);
            return CurlCode.Ok;
        }

        ReplaceData(new EasyMimeFile(path));
        fileName = Path.GetFileName(path);
        return File.Exists(path) || Directory.Exists(path) ? CurlCode.Ok : CurlCode.ReadError;
    }

    /// <summary><c>curl_mime_data_cb</c>: the read callback's content, or no data when <paramref name="read" /> is <see langword="null" />.</summary>
    /// <param name="size">The content's length, or -1 when unknown.</param>
    /// <param name="read">The read callback, or <see langword="null" />.</param>
    /// <param name="seek">The seek callback, or <see langword="null" />.</param>
    /// <returns><see cref="CurlCode.Ok" />.</returns>
    public CurlCode SetDataCallback(long size, ReadCallback? read, SeekCallback? seek)
    {
        ReplaceData(read is null ? null : new EasyMimeCallback(size, read, seek));
        return CurlCode.Ok;
    }

    /// <summary>
    /// <c>curl_mime_subparts</c>: <paramref name="subParts" />, built when this part is, as the part's content;
    /// <see langword="null" /> clears the data. Setting the body the part already holds changes nothing.
    /// </summary>
    /// <param name="subParts">The nested body, or <see langword="null" />.</param>
    /// <returns>
    /// <see cref="CurlCode.Ok" />, or <see cref="CurlCode.BadFunctionArgument" /> for a body of another engine,
    /// one another part already holds, or one this part is inside, which would nest the part in itself.
    /// </returns>
    public CurlCode SetSubParts(IMimeBuilder? subParts)
    {
        if (subParts is null)
        {
            ReplaceData(null);
            return CurlCode.Ok;
        }

        if (ReferenceEquals(subParts, this.subParts))
        {
            return CurlCode.Ok;
        }

        if (subParts is not ManagedMimeBuilder body || body.Parent is not null || Encloses(body))
        {
            return CurlCode.BadFunctionArgument;
        }

        ReplaceData(null);
        body.Parent = this;
        this.subParts = body;
        return CurlCode.Ok;
    }

    /// <summary><c>curl_mime_headers</c>: the header lines, copied, or none for <see langword="null" />.</summary>
    /// <param name="headers">The header lines, or <see langword="null" />.</param>
    /// <returns><see cref="CurlCode.Ok" />.</returns>
    public CurlCode SetHeaders(IReadOnlyList<string>? headers)
    {
        this.headers = headers is null ? [] : [.. headers];
        return CurlCode.Ok;
    }

    /// <summary>The part as its setters have left it, a nested body built now.</summary>
    /// <returns>The part.</returns>
    internal EasyMimePart Build() =>
        new()
        {
            Name = name,
            FileName = fileName,
            Type = type,
            Encoder = encoder,
            Headers = headers,
            Data = data,
            SubParts = subParts?.Build(),
        };

    /// <summary>
    /// Whether <paramref name="body" /> is the body this part is in, or one a part enclosing it is in: libcurl's
    /// walk up from <c>part->parent</c> through each body's parent part.
    /// </summary>
    /// <param name="body">The body to look for.</param>
    /// <returns>Whether this part is inside <paramref name="body" />.</returns>
    private bool Encloses(ManagedMimeBuilder body)
    {
        for (ManagedMimeBuilder? enclosing = owner; enclosing is not null; enclosing = enclosing.Parent?.Owner)
        {
            if (ReferenceEquals(enclosing, body))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the body the part was added to.</summary>
    internal ManagedMimeBuilder Owner => owner;

    /// <summary>Replaces the part's data source, freeing a nested body it held of this part.</summary>
    /// <param name="replacement">The new data, or <see langword="null" /> for none.</param>
    private void ReplaceData(EasyMimeData? replacement)
    {
        if (subParts is not null)
        {
            subParts.Parent = null;
            subParts = null;
        }

        data = replacement;
    }
}
