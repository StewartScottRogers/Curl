namespace Curl.Core.Multipart;

/// <summary>
/// Where a <see cref="MultipartFormPart" /> takes its body from: the four forms of the
/// <c>-F</c>/<c>--form</c> mini-language, and the two of libcurl's MIME API no command line
/// gives, <c>curl_mime_data</c>'s bytes and <c>curl_mime_data_cb</c>'s read callback.
/// </summary>
public enum MultipartFormPartKind
{
    /// <summary><c>name=value</c>: <see cref="MultipartFormPart.Content" /> is the text sent as the body.</summary>
    Text = 0,

    /// <summary>
    /// <c>name=@file</c>: the file at <see cref="MultipartFormPart.Content" /> is the body, and
    /// the part carries a file name, the file's own unless <see cref="MultipartFormPart.FileName" />
    /// gives one.
    /// </summary>
    FileUpload,

    /// <summary>
    /// <c>name=&lt;file</c>: the bytes of the file at <see cref="MultipartFormPart.Content" /> are
    /// the body, with no file name.
    /// </summary>
    FileContent,

    /// <summary>A part whose body is its own <see cref="MultipartFormPart.Parts" />, sent as a nested multipart.</summary>
    Multipart,

    /// <summary>
    /// <c>curl_mime_data</c>: <see cref="MultipartFormPart.Bytes" /> is the body, sent as it is,
    /// whatever bytes it holds; its headers are chosen as a <see cref="Text" /> part's are.
    /// </summary>
    Bytes,

    /// <summary>
    /// <c>curl_mime_data_cb</c>: the stream <see cref="MultipartFormPart.OpenContent" /> opens is the
    /// body, read while the body is sent, never ahead; <see cref="MultipartFormPart.ContentLength" />
    /// is its size, or <see langword="null" /> when unknown, which sends the body chunked. Its headers
    /// are chosen as a <see cref="Text" /> part's are.
    /// </summary>
    Reader,
}
