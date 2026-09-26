namespace Curl.Cli;

/// <summary>
/// Where a <see cref="FormPartSpecification"/> takes its content from, as the <c>-F</c> /
/// <c>--form</c> mini-language says it.
/// </summary>
public enum FormPartKind
{
    /// <summary>
    /// <c>name=value</c>, or any <c>--form-string</c>: <see cref="FormPartSpecification.Content"/> is
    /// the text sent as the part's body.
    /// </summary>
    Text = 0,

    /// <summary>
    /// <c>name=@file</c>: the file at <see cref="FormPartSpecification.Content"/> is uploaded as the
    /// body, and the part carries a file name (the file's own, unless <c>;filename=</c> gives one).
    /// </summary>
    FileUpload,

    /// <summary>
    /// <c>name=&lt;file</c>: the bytes of the file at <see cref="FormPartSpecification.Content"/> are
    /// sent as the body, with no file name.
    /// </summary>
    FileContent,

    /// <summary>
    /// A part that holds <see cref="FormPartSpecification.Parts"/> of its own: opened by
    /// <c>name=(</c> and closed by <c>=)</c>, or made by <c>name=@file1,file2</c> to hold the files.
    /// </summary>
    Multipart,
}
