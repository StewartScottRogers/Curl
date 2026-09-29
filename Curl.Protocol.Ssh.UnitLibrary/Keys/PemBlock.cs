namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// One PEM block of a key file (RFC 7468, with RFC 1421's headers): its label, the
/// <c>Name: value</c> headers before its body, such as legacy encryption's
/// <c>Proc-Type</c> and <c>DEK-Info</c>, and the body's bytes.
/// </summary>
/// <param name="Label">The label between <c>-----BEGIN </c> and <c>-----</c>, such as <c>RSA PRIVATE KEY</c>.</param>
/// <param name="Headers">The headers, by name, compared ordinally.</param>
/// <param name="Body">The base64 body, decoded.</param>
internal sealed record PemBlock(string Label, IReadOnlyDictionary<string, string> Headers, byte[] Body)
{
    private const string BeginPrefix = "-----BEGIN ";

    private const string Dashes = "-----";

    /// <summary>
    /// Finds the first block in <paramref name="text" />: the first <c>-----BEGIN
    /// &lt;label&gt;-----</c> line, the headers that follow it, and the base64 lines up to
    /// the matching <c>-----END &lt;label&gt;-----</c>. Lines are trimmed and blank lines skipped.
    /// </summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The block, or <see langword="null" /> when there is none or it never ends.</returns>
    /// <exception cref="FormatException">The body is not base64.</exception>
    internal static PemBlock? Find(string text)
    {
        string[] lines = [.. text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0)];
        int begin = Array.FindIndex(lines, IsBeginLine);
        if (begin < 0)
        {
            return null;
        }

        string label = lines[begin][BeginPrefix.Length..^Dashes.Length];
        int end = Array.IndexOf(lines, "-----END " + label + Dashes, begin + 1);
        return end < 0 ? null : Read(label, lines[(begin + 1)..end]);
    }

    private static bool IsBeginLine(string line) =>
        line.Length > BeginPrefix.Length + Dashes.Length && line.StartsWith(BeginPrefix, StringComparison.Ordinal) && line.EndsWith(Dashes, StringComparison.Ordinal);

    // Leading lines with a colon past their first character are headers; the rest is base64.
    private static PemBlock Read(string label, string[] inner)
    {
        Dictionary<string, string> headers = new(StringComparer.Ordinal);
        int bodyStart = 0;
        for (; bodyStart < inner.Length && inner[bodyStart].IndexOf(':', StringComparison.Ordinal) is > 0 and int colon; bodyStart++)
        {
            headers[inner[bodyStart][..colon].Trim()] = inner[bodyStart][(colon + 1)..].Trim();
        }

        return new PemBlock(label, headers, Convert.FromBase64String(string.Concat(inner[bodyStart..])));
    }
}
