using Curl.Api.Callbacks;

namespace Curl.Api.Options.Http;

/// <summary>
/// <c>curl_mime_data_cb</c>: a part's content produced by a read callback while the
/// transfer sends it. libcurl's <c>arg</c> is the delegates' closure, and its
/// <c>freefunc</c> has no counterpart: the closure's own lifetime ends it.
/// </summary>
/// <param name="Size">
/// <c>datasize</c>: the content's length in bytes, or -1 when it is unknown, which needs
/// chunked transfer encoding.
/// </param>
/// <param name="Read"><c>readfunc</c>: fills the buffer with the next bytes.</param>
/// <param name="Seek">
/// <c>seekfunc</c>: rewinds the content when libcurl must send it again, or
/// <see langword="null"/> when it cannot be rewound.
/// </param>
public sealed record EasyMimeCallback(long Size, ReadCallback Read, SeekCallback? Seek) : EasyMimeData;
