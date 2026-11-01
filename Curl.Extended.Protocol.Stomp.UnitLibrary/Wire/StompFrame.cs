namespace Curl.Extended.Protocol.Stomp.Wire;

/// <summary>One header line of a STOMP frame, its name and value unescaped (STOMP 1.2, "Value Encoding").</summary>
/// <param name="Name">The header's name.</param>
/// <param name="Value">The header's value.</param>
internal sealed record StompHeader(string Name, string Value);

/// <summary>One STOMP 1.2 frame: a command, its headers in the order sent, and its body (STOMP 1.2, "STOMP Frames").</summary>
/// <param name="Command">The command, such as <c>CONNECT</c> or <c>MESSAGE</c>.</param>
/// <param name="Headers">The headers, in the order they appear on the wire.</param>
/// <param name="Body">The body, empty when the frame has none.</param>
internal sealed record StompFrame(string Command, IReadOnlyList<StompHeader> Headers, byte[] Body)
{
    /// <summary>
    /// Gives the value of the first header named <paramref name="name"/>, or <see langword="null"/> when there is none:
    /// a repeated header's first entry is the one that counts (STOMP 1.2, "Repeated Header Entries").
    /// </summary>
    /// <param name="name">The header's name, matched case-sensitively.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    internal string? HeaderValue(string name) =>
        Headers.FirstOrDefault(header => string.Equals(header.Name, name, StringComparison.Ordinal))?.Value;
}
