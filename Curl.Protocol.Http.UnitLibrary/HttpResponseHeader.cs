namespace Curl.Protocol.Http;

/// <summary>
/// One response header field, after any continuation lines were folded into it.
/// </summary>
/// <param name="name">The text before the first colon, exactly as received.</param>
/// <param name="value">The text after the first colon, without leading or trailing blanks.</param>
internal sealed class HttpResponseHeader(string name, string value)
{
    /// <summary>
    /// Gets the text before the first colon, exactly as received.
    /// </summary>
    internal string Name { get; } = name;

    /// <summary>
    /// Gets the text after the first colon, without leading or trailing blanks.
    /// </summary>
    internal string Value { get; } = value;
}
