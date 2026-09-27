namespace Curl.Core.Multipart;

/// <summary>
/// One part of a <c>multipart/form-data</c> request body, as <see cref="MultipartFormBodyBuilder" />
/// takes it: the part's name, where its body comes from, and the <c>;type=</c>,
/// <c>;filename=</c>, <c>;headers=</c> and <c>;encoder=</c> given with it.
/// </summary>
/// <param name="Name">
/// The field name; <see langword="null" /> sends the part with no <c>name=</c> in its
/// <c>Content-Disposition</c>.
/// </param>
/// <param name="Kind">Where the body comes from.</param>
/// <param name="Content">
/// The text of a <see cref="MultipartFormPartKind.Text" /> part; the path of the file of a
/// <see cref="MultipartFormPartKind.FileUpload" /> or <see cref="MultipartFormPartKind.FileContent" />
/// part; ignored for a <see cref="MultipartFormPartKind.Multipart" /> part.
/// </param>
/// <param name="ContentType">The <c>;type=</c> value; <see langword="null" /> lets the builder choose, as curl does.</param>
/// <param name="FileName">
/// The <c>;filename=</c> value; <see langword="null" /> means the file's own name for a
/// <see cref="MultipartFormPartKind.FileUpload" /> part and no file name for any other.
/// </param>
/// <param name="Headers">The part's own header lines, in order, sent after the ones the builder writes.</param>
/// <param name="Parts">The parts inside a <see cref="MultipartFormPartKind.Multipart" /> part; empty for any other kind.</param>
public sealed record MultipartFormPart(
    string? Name,
    MultipartFormPartKind Kind,
    string Content,
    string? ContentType,
    string? FileName,
    IReadOnlyList<string> Headers,
    IReadOnlyList<MultipartFormPart> Parts)
{
    /// <summary>
    /// Gets the <c>;encoder=</c> value, unchecked: <c>binary</c>, <c>8bit</c>, <c>7bit</c>,
    /// <c>base64</c> or <c>quoted-printable</c> in any case encodes the part's body and names the
    /// encoding in its <c>Content-Transfer-Encoding</c>, and any other name fails the build with
    /// exit 43, as curl 8.21.0 does. <see langword="null" />, the default, sends the body as it
    /// is. Ignored for a <see cref="MultipartFormPartKind.Multipart" /> part, which the <c>-F</c>
    /// syntax never gives an encoder.
    /// </summary>
    public string? Encoder { get; init; }
}
