namespace Curl.Cli;

/// <summary>
/// One part of the multipart form <c>-F</c> / <c>--form</c> and <c>--form-string</c> build, as the
/// command line described it: its name, where its content comes from, and the <c>;type=</c>,
/// <c>;filename=</c>, <c>;encoder=</c> and <c>;headers=</c> given with it. Nothing here has been
/// read or encoded: a file is named, not opened, and the name is kept as typed.
/// </summary>
public sealed class FormPartSpecification
{
    private readonly List<FormPartSpecification> parts = [];

    internal FormPartSpecification(FormPartKind kind, string content, string? contentType, string? fileName, string? encoder, IReadOnlyList<string> headers)
    {
        Kind = kind;
        Content = content;
        ContentType = contentType;
        FileName = fileName;
        Encoder = encoder;
        Headers = headers;
    }

    /// <summary>
    /// The field name, the text before the first <c>=</c>; <see langword="null"/> when that text is
    /// empty (<c>-F =value</c>), which sends the part with no name. Only the outermost part of
    /// <c>name=@file1,file2</c> is named; the file parts inside it are not.
    /// </summary>
    public string? Name { get; internal set; }

    /// <summary>Where the part's content comes from.</summary>
    public FormPartKind Kind { get; }

    /// <summary>
    /// The text of a <see cref="FormPartKind.Text"/> part; the path of the file of a
    /// <see cref="FormPartKind.FileUpload"/> or <see cref="FormPartKind.FileContent"/> part, where
    /// <c>-</c> means standard input; empty for a <see cref="FormPartKind.Multipart"/> part.
    /// </summary>
    public string Content { get; }

    /// <summary>The <c>;type=</c> value, with any parameters that followed it; <see langword="null"/> when none was given.</summary>
    public string? ContentType { get; }

    /// <summary>The <c>;filename=</c> value; <see langword="null"/> when none was given.</summary>
    public string? FileName { get; }

    /// <summary>The <c>;encoder=</c> value, unchecked; <see langword="null"/> when none was given.</summary>
    public string? Encoder { get; }

    /// <summary>The part's own header lines, from <c>;headers=</c> values and files, in the order given.</summary>
    public IReadOnlyList<string> Headers { get; }

    /// <summary>The parts inside a <see cref="FormPartKind.Multipart"/> part, in order; empty for any other kind.</summary>
    public IReadOnlyList<FormPartSpecification> Parts => parts;

    /// <summary>Appends <paramref name="part"/> to <see cref="Parts"/>.</summary>
    /// <param name="part">The part to append.</param>
    internal void AddPart(FormPartSpecification part) => parts.Add(part);
}
