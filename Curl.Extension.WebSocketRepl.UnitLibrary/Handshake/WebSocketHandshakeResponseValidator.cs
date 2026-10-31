namespace Curl.Extension.WebSocketRepl.Handshake;

/// <summary>
/// Checks the server's opening handshake response against what RFC 6455 section 4.1 requires of the client:
/// status 101, <c>Upgrade: websocket</c>, a <c>Connection</c> holding <c>Upgrade</c>, the
/// <c>Sec-WebSocket-Accept</c> of section 4.2.2, and no extension or subprotocol, since the client asked for none.
/// </summary>
public static class WebSocketHandshakeResponseValidator
{
    /// <summary>Finds the first requirement the response breaks.</summary>
    /// <param name="responseHead">The response's status line and header lines, without the blank line that ends them.</param>
    /// <param name="key">The <c>Sec-WebSocket-Key</c> the client sent.</param>
    /// <returns>Why the client fails the connection, or <see langword="null" /> when the response is valid.</returns>
    public static string? FindFailure(string responseHead, string key)
    {
        ArgumentNullException.ThrowIfNull(responseHead);
        string[] lines = responseHead.Split("\r\n");
        if (!IsSwitchingProtocols(lines[0]))
        {
            return $"the server answered \"{lines[0]}\", not 101 Switching Protocols";
        }

        Dictionary<string, List<string>>? fields = ReadFields(lines);
        return fields is null ? "the response has a header line with no field name and colon" : FindFieldFailure(fields, key);
    }

    private static bool IsSwitchingProtocols(string statusLine)
    {
        string[] parts = statusLine.Split(' ');
        return parts.Length >= 2 && parts[0].StartsWith("HTTP/", StringComparison.Ordinal) && parts[1] == "101";
    }

    private static Dictionary<string, List<string>>? ReadFields(string[] lines)
    {
        Dictionary<string, List<string>> fields = new(StringComparer.OrdinalIgnoreCase);
        foreach (string line in lines.AsSpan(1))
        {
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                return null;
            }

            string name = line[..colon];
            if (!fields.TryGetValue(name, out List<string>? values))
            {
                values = [];
                fields.Add(name, values);
            }

            values.Add(line[(colon + 1)..].Trim(' ', '\t'));
        }

        return fields;
    }

    private static string? FindFieldFailure(Dictionary<string, List<string>> fields, string key)
    {
        if (!IsOneValue(Values(fields, "Upgrade"), "websocket", StringComparison.OrdinalIgnoreCase))
        {
            return "the response's Upgrade is not websocket";
        }

        if (!HoldsUpgradeToken(Values(fields, "Connection")))
        {
            return "the response's Connection does not hold Upgrade";
        }

        if (!IsOneValue(Values(fields, "Sec-WebSocket-Accept"), WebSocketHandshakeKey.ComputeAccept(key), StringComparison.Ordinal))
        {
            return "the response's Sec-WebSocket-Accept does not answer the key sent";
        }

        return HasExtensionOrSubprotocol(fields) ? "the server chose an extension or subprotocol the client did not ask for" : null;
    }

    private static List<string> Values(Dictionary<string, List<string>> fields, string name) =>
        fields.TryGetValue(name, out List<string>? values) ? values : [];

    private static bool IsOneValue(List<string> values, string expected, StringComparison comparison) =>
        values.Count == 1 && values[0].Equals(expected, comparison);

    private static bool HoldsUpgradeToken(List<string> values)
    {
        foreach (string value in values)
        {
            foreach (string token in value.Split(','))
            {
                if (token.Trim(' ', '\t').Equals("Upgrade", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasExtensionOrSubprotocol(Dictionary<string, List<string>> fields) =>
        fields.ContainsKey("Sec-WebSocket-Extensions") || fields.ContainsKey("Sec-WebSocket-Protocol");
}
