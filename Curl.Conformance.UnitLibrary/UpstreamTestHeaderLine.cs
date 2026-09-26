namespace Curl.Conformance;

/// <summary>
/// The guess <c>subnewlines</c> in upstream's <c>testutil.pm</c> at <c>curl-8_21_0</c> makes for
/// <c>crlf="headers"</c>: whether one line (with its line feed, if any) is a header line.
/// </summary>
internal static class UpstreamTestHeaderLine
{
    private static readonly string[] HttpMethods = ["GET", "HEAD", "POST", "PUT", "DELETE", "CONNECT"];

    private static readonly string[] RtspMethods = ["SETUP", "GET_PARAMETER", "OPTIONS", "ANNOUNCE", "DESCRIBE"];

    /// <summary>Whether upstream would treat the line as a header line.</summary>
    /// <param name="line">One line, read as Latin-1, with its line feed if it has one.</param>
    /// <returns><see langword="true"/> for a header line.</returns>
    public static bool LooksLikeHeader(string line) =>
        IsStatusLine(line)
        || IsRequestLine(line, HttpMethods, "HTTP/")
        || IsRequestLine(line, RtspMethods, "RTSP/")
        || IsHeaderField(line);

    // ^HTTP\/(1.1|1.0|2|3) ([1-5]|9)[^\x0d]*\z  (the dots match any character)
    private static bool IsStatusLine(string line)
    {
        if (!line.StartsWith("HTTP/", StringComparison.Ordinal) || line.Contains('\r', StringComparison.Ordinal))
        {
            return false;
        }

        string rest = line[5..];
        int versionLength = StatusLineVersionLength(rest);
        return versionLength > 0
            && rest.Length > versionLength + 1
            && rest[versionLength] == ' '
            && "123459".Contains(rest[versionLength + 1], StringComparison.Ordinal);
    }

    private static int StatusLineVersionLength(string rest)
    {
        if (rest.Length > 2 && rest[0] == '1' && "10".Contains(rest[2], StringComparison.Ordinal))
        {
            return 3;
        }

        return rest.Length > 0 && "23".Contains(rest[0], StringComparison.Ordinal) ? 1 : 0;
    }

    // ^(METHOD) \S+ PROTOCOL\d+
    private static bool IsRequestLine(string line, string[] methods, string protocol) =>
        methods.Any(method => line.StartsWith(method + " ", StringComparison.Ordinal) && HasTargetThenProtocol(line[(method.Length + 1)..], protocol));

    private static bool HasTargetThenProtocol(string rest, string protocol)
    {
        int targetLength = rest.TakeWhile(character => character is not (' ' or '\t' or '\n' or '\r' or '\f' or '\v')).Count();
        string afterTarget = rest[targetLength..];
        return targetLength > 0
            && afterTarget.StartsWith(" " + protocol, StringComparison.Ordinal)
            && afterTarget.Length > protocol.Length + 1
            && char.IsAsciiDigit(afterTarget[protocol.Length + 1]);
    }

    // ^[a-z0-9_-]+: [^\x0d]*\z  (case-insensitive), and not ^curl: \(\d+\)
    private static bool IsHeaderField(string line)
    {
        int separator = line.IndexOf(": ", StringComparison.Ordinal);
        return separator > 0
            && line[..separator].All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-')
            && !line.Contains('\r', StringComparison.Ordinal)
            && !IsCurlError(line);
    }

    private static bool IsCurlError(string line)
    {
        if (!line.StartsWith("curl: (", StringComparison.Ordinal))
        {
            return false;
        }

        string rest = line[7..];
        int digits = rest.TakeWhile(char.IsAsciiDigit).Count();
        return digits > 0 && rest[digits..].StartsWith(") ", StringComparison.Ordinal);
    }
}
