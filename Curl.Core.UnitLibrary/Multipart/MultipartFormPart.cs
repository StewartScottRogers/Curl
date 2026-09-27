namespace Curl.Core.Multipart;

/// <summary>
/// One part of a <c>multipart/form-data</c> request body, as <see cref="MultipartFormBodyBuilder" />
/// takes it: the part's name, where its body comes from, and the <c>;type=</c>,
/// <c>;filename=</c> and <c>;headers=</c> given with it.
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
    IReadOnlyList<MultipartFormPart> Parts);
