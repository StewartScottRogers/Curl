using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;

namespace Libcurl.Mime;

/// <summary>
/// One part of a <see cref="CurlMime"/> (<c>curl_mimepart</c>), created by
/// <see cref="CurlMime.AddPart"/>: the <c>curl_mime_*</c> part setters, each a call to the engine's
/// <see cref="IMimePartBuilder"/> answering its <see cref="CurlCode"/> (ADR-0583). Setting a data source
/// replaces the one set before. Every setter throws <see cref="ObjectDisposedException"/> once the part's
/// handle has been freed.
/// </summary>
public sealed class CurlMimePart
{
    private readonly CurlMime owner;
    private readonly IMimePartBuilder builder;

    /// <summary>Wraps <paramref name="builder"/>, a part of <paramref name="owner"/>.</summary>
    /// <param name="owner">The handle the part belongs to.</param>
    /// <param name="builder">The engine's part builder.</param>
    internal CurlMimePart(CurlMime owner, IMimePartBuilder builder)
    {
        this.owner = owner;
        this.builder = builder;
    }

    /// <summary><c>curl_mime_name</c>: the form field name.</summary>
    /// <param name="name">The name, or <see langword="null"/> for none.</param>
    /// <returns>The engine's answer.</returns>
    public CurlCode SetName(string? name) => Live().SetName(name);

    /// <summary><c>curl_mime_filename</c>: the remote file name sent in <c>Content-Disposition</c>.</summary>
    /// <param name="fileName">The file name, or <see langword="null"/> for none.</param>
    /// <returns>The engine's answer.</returns>
    public CurlCode SetFileName(string? fileName) => Live().SetFileName(fileName);

    /// <summary><c>curl_mime_type</c>: the <c>Content-Type</c>.</summary>
    /// <param name="type">The type, or <see langword="null"/> for libcurl's guess.</param>
    /// <returns>The engine's answer.</returns>
    public CurlCode SetType(string? type) => Live().SetType(type);

    /// <summary><c>curl_mime_encoder</c>: the transfer encoding.</summary>
    /// <param name="encoding"><c>binary</c>, <c>8bit</c>, <c>7bit</c>, <c>base64</c> or <c>quoted-printable</c>, or <see langword="null"/> for none.</param>
    /// <returns>The engine's answer; <see cref="CurlCode.BadFunctionArgument"/> for any other encoding.</returns>
    public CurlCode SetEncoder(string? encoding) => Live().SetEncoder(encoding);

    /// <summary><c>curl_mime_data</c>: the part's content, copied.</summary>
    /// <param name="data">The bytes, or <see langword="null"/> for no data.</param>
    /// <returns>The engine's answer.</returns>
    public CurlCode SetData(byte[]? data) => Live().SetData(data);

    /// <summary><c>curl_mime_data</c> with <c>CURL_ZERO_TERMINATED</c>: the part's content as the text's UTF-8 bytes.</summary>
    /// <param name="text">The text, or <see langword="null"/> for no data.</param>
    /// <returns>The engine's answer.</returns>
    public CurlCode SetData(string? text) => Live().SetData(text is null ? null : Encoding.UTF8.GetBytes(text));

    /// <summary><c>curl_mime_filedata</c>: the part's content read from a file, whose base name becomes the file name.</summary>
    /// <param name="path">The file's path, or <see langword="null"/> for no data.</param>
    /// <returns>The engine's answer; <see cref="CurlCode.ReadError"/> when the file cannot be read.</returns>
    public CurlCode SetFileData(string? path) => Live().SetFileData(path);

    /// <summary><c>curl_mime_data_cb</c>: the part's content produced by a read callback; <c>arg</c> is the delegates' closure and <c>freefunc</c> has no counterpart.</summary>
    /// <param name="size"><c>datasize</c>: the content's length, or -1 when unknown.</param>
    /// <param name="read"><c>readfunc</c>: fills the buffer with the next bytes, or <see langword="null"/> for no data.</param>
    /// <param name="seek"><c>seekfunc</c>: rewinds the content, or <see langword="null"/> when it cannot be rewound.</param>
    /// <returns>The engine's answer.</returns>
    public CurlCode SetDataCallback(long size, ReadCallback? read, SeekCallback? seek) => Live().SetDataCallback(size, read, seek);

    /// <summary><c>curl_mime_subparts</c>: a nested multipart body sent as the part's content.</summary>
    /// <param name="subParts">The nested handle, or <see langword="null"/> for none.</param>
    /// <returns>The engine's answer; <see cref="CurlCode.BadFunctionArgument"/> when it would nest the part inside itself.</returns>
    /// <exception cref="ObjectDisposedException">The part's handle or <paramref name="subParts"/> has been freed.</exception>
    public CurlCode SetSubParts(CurlMime? subParts) => Live().SetSubParts(subParts?.Builder);

    /// <summary><c>curl_mime_headers</c>: the part's extra headers, copied; <c>take_ownership</c> has no counterpart.</summary>
    /// <param name="headers">The header lines, such as a <c>CurlSlist</c>, or <see langword="null"/> for none.</param>
    /// <returns>The engine's answer.</returns>
    public CurlCode SetHeaders(IEnumerable<string>? headers) => Live().SetHeaders(headers is null ? null : [.. headers]);

    private IMimePartBuilder Live()
    {
        owner.ThrowIfFreed();
        return builder;
    }
}
