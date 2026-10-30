using System.Globalization;
using System.Text;

namespace Curl.Extension.Masque.Wire;

/// <summary>
/// The HTTP/1.1 upgrade that opens a MASQUE tunnel: the request of RFC 9298 section 3.2 and
/// RFC 9484 section 4.5, and the check of the reply of RFC 9298 section 3.3 and RFC 9484
/// section 4.6.
/// </summary>
internal static class MasqueUpgrade
{
    /// <summary>
    /// Gets the upgrade token for a tunnel kind.
    /// </summary>
    /// <param name="kind">The tunnel kind.</param>
    /// <returns><c>connect-udp</c> or <c>connect-ip</c>.</returns>
    public static string UpgradeToken(MasqueTunnelKind kind) => kind == MasqueTunnelKind.ConnectUdp ? "connect-udp" : "connect-ip";

    /// <summary>
    /// Builds the upgrade request: <c>GET</c> in absolute form, as the RFCs' examples send it,
    /// with <c>Host</c>, <c>Connection: Upgrade</c>, <c>Upgrade</c> and <c>Capsule-Protocol: ?1</c>.
    /// </summary>
    /// <param name="target">The expanded URI template.</param>
    /// <param name="kind">The tunnel kind.</param>
    /// <returns>The request's bytes, the empty line included.</returns>
    public static byte[] BuildRequest(Uri target, MasqueTunnelKind kind)
    {
        var request = string.Create(
            CultureInfo.InvariantCulture,
            $"GET {target.AbsoluteUri} HTTP/1.1\r\nHost: {target.Authority}\r\nConnection: Upgrade\r\nUpgrade: {UpgradeToken(kind)}\r\nCapsule-Protocol: ?1\r\n\r\n");
        return Encoding.ASCII.GetBytes(request);
    }

    /// <summary>
    /// Checks the proxy's reply head: the tunnel opens only on <c>101</c> with the tunnel's
    /// <c>Upgrade</c> token and <c>Connection: Upgrade</c>.
    /// </summary>
    /// <param name="head">The status line and header lines, separated by CRLF.</param>
    /// <param name="kind">The tunnel kind asked for.</param>
    /// <exception cref="MasqueException">The proxy refused the upgrade (with its status) or answered 101 without the headers it must send.</exception>
    public static void CheckReply(string head, MasqueTunnelKind kind)
    {
        var token = UpgradeToken(kind);
        var lines = head.Split("\r\n");
        var status = ParseStatus(lines[0]);
        if (status != 101)
        {
            throw new MasqueException($"MASQUE proxy refused the {token} upgrade with status {status}", status);
        }

        if (!HasHeaderToken(lines, "Upgrade", token) || !HasHeaderToken(lines, "Connection", "Upgrade"))
        {
            throw new MasqueException($"MASQUE proxy answered 101 without Upgrade: {token} and Connection: Upgrade", status);
        }
    }

    private static int ParseStatus(string statusLine)
    {
        var parts = statusLine.Split(' ', 3);
        if (parts.Length < 2 || !parts[0].StartsWith("HTTP/1.", StringComparison.Ordinal) || parts[1].Length != 3
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var status))
        {
            throw new MasqueException($"MASQUE proxy sent a malformed status line: {statusLine}");
        }

        return status;
    }

    // Whether a header named name lists token among its comma-separated values, ignoring case.
    private static bool HasHeaderToken(string[] lines, string name, string token) =>
        lines.Skip(1)
            .Select(line => line.Split(':', 2))
            .Where(field => field.Length == 2 && field[0].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
            .SelectMany(field => field[1].Split(','))
            .Any(value => value.Trim().Equals(token, StringComparison.OrdinalIgnoreCase));
}
