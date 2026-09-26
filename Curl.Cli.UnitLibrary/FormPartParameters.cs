namespace Curl.Cli;

/// <summary>
/// The content and parameters of one form part, as <see cref="FormPartParameterReader.Read"/> found them.
/// </summary>
/// <param name="data">The content word: the text, or the file name.</param>
/// <param name="contentType">The <c>;type=</c> value with any parameters after it, or <see langword="null"/>.</param>
/// <param name="fileName">The <c>;filename=</c> value, or <see langword="null"/>.</param>
/// <param name="encoder">The <c>;encoder=</c> value, or <see langword="null"/>.</param>
/// <param name="headers">The <c>;headers=</c> lines, in order.</param>
/// <param name="separator">The character that ended the part, <c>\0</c> at the end of the value.</param>
internal sealed class FormPartParameters(string data, string? contentType, string? fileName, string? encoder, IReadOnlyList<string> headers, char separator)
{
    /// <summary>The content word: the text, or the file name.</summary>
    public string Data => data;

    /// <summary>The <c>;type=</c> value with any parameters after it, or <see langword="null"/>.</summary>
    public string? ContentType => contentType;

    /// <summary>The <c>;filename=</c> value, or <see langword="null"/>.</summary>
    public string? FileName => fileName;

    /// <summary>The <c>;encoder=</c> value, or <see langword="null"/>.</summary>
    public string? Encoder => encoder;

    /// <summary>The <c>;headers=</c> lines, in order.</summary>
    public IReadOnlyList<string> Headers => headers;

    /// <summary>The character that ended the part, <c>\0</c> at the end of the value.</summary>
    public char Separator => separator;
}
