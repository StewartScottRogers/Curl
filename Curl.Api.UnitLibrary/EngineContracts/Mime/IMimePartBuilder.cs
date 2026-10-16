using Curl.Api.Callbacks;
using Curl.Api.Codes;

namespace Curl.Api.EngineContracts.Mime;

/// <summary>
/// One part of an <see cref="IMimeBuilder"/> (<c>curl_mimepart</c>): the <c>curl_mime_*</c>
/// setters, each giving the <see cref="Options.Http.EasyMimePart"/> property of the same name.
/// Setting a data source replaces the one set before.
/// </summary>
public interface IMimePartBuilder
{
    /// <summary><c>curl_mime_name</c>: the form field name.</summary>
    /// <param name="name">The name, or <see langword="null"/> for none.</param>
    /// <returns>The call's result.</returns>
    CurlCode SetName(string? name);

    /// <summary><c>curl_mime_filename</c>: the remote file name sent in <c>Content-Disposition</c>.</summary>
    /// <param name="fileName">The file name, or <see langword="null"/> for none.</param>
    /// <returns>The call's result.</returns>
    CurlCode SetFileName(string? fileName);

    /// <summary><c>curl_mime_type</c>: the <c>Content-Type</c>.</summary>
    /// <param name="type">The type, or <see langword="null"/> for libcurl's guess.</param>
    /// <returns>The call's result.</returns>
    CurlCode SetType(string? type);

    /// <summary><c>curl_mime_encoder</c>: the transfer encoding.</summary>
    /// <param name="encoding"><c>binary</c>, <c>8bit</c>, <c>7bit</c>, <c>base64</c> or <c>quoted-printable</c>, or <see langword="null"/> for none.</param>
    /// <returns>The call's result; <see cref="CurlCode.BadFunctionArgument"/> for any other encoding.</returns>
    CurlCode SetEncoder(string? encoding);

    /// <summary><c>curl_mime_data</c>: the part's content, copied.</summary>
    /// <param name="data">The bytes, or <see langword="null"/> for no data.</param>
    /// <returns>The call's result.</returns>
    CurlCode SetData(byte[]? data);

    /// <summary><c>curl_mime_filedata</c>: the part's content read from a file, whose base name becomes the file name.</summary>
    /// <param name="path">The file's path, or <see langword="null"/> for no data.</param>
    /// <returns>The call's result; <see cref="CurlCode.ReadError"/> when the file cannot be read.</returns>
    CurlCode SetFileData(string? path);

    /// <summary><c>curl_mime_data_cb</c>: the part's content produced by a read callback; <c>arg</c> is the delegates' closure and <c>freefunc</c> has no counterpart.</summary>
    /// <param name="size"><c>datasize</c>: the content's length, or -1 when unknown.</param>
    /// <param name="read"><c>readfunc</c>: fills the buffer with the next bytes, or <see langword="null"/> for no data.</param>
    /// <param name="seek"><c>seekfunc</c>: rewinds the content, or <see langword="null"/> when it cannot be rewound.</param>
    /// <returns>The call's result.</returns>
    CurlCode SetDataCallback(long size, ReadCallback? read, SeekCallback? seek);

    /// <summary><c>curl_mime_subparts</c>: a nested multipart body sent as the part's content.</summary>
    /// <param name="subParts">The nested body's builder, or <see langword="null"/> for none.</param>
    /// <returns>The call's result; <see cref="CurlCode.BadFunctionArgument"/> when it would nest the part inside itself.</returns>
    CurlCode SetSubParts(IMimeBuilder? subParts);

    /// <summary><c>curl_mime_headers</c>: the part's extra headers; <c>take_ownership</c> has no counterpart.</summary>
    /// <param name="headers">The header lines, or <see langword="null"/> for none.</param>
    /// <returns>The call's result.</returns>
    CurlCode SetHeaders(IReadOnlyList<string>? headers);
}
