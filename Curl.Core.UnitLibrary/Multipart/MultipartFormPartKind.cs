namespace Curl.Core.Multipart;

/// <summary>
/// Where a <see cref="MultipartFormPart" /> takes its body from: the four forms of the
/// <c>-F</c>/<c>--form</c> mini-language.
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
}
