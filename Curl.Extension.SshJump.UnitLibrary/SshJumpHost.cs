using System.Globalization;

namespace Curl.Extension.SshJump;

/// <summary>
/// The SSH jump host <c>--ssh-jump</c> names, <c>user[:password]@host[:port]</c> as OpenSSH's <c>ProxyJump</c>
/// writes it (ADR-1167): the user before the last <c>@</c>, an optional password after its first <c>:</c>, the
/// host (an IPv6 address in brackets) and an optional port, 22 when none is given.
/// </summary>
/// <param name="User">The user the jump host authenticates.</param>
/// <param name="Password">The password, or <see langword="null" /> to authenticate with the agent and key files alone.</param>
/// <param name="Host">The jump host's name or address, without brackets.</param>
/// <param name="Port">The jump host's SSH port.</param>
public sealed record SshJumpHost(string User, string? Password, string Host, int Port)
{
    /// <summary>The SSH port, used when the value names none.</summary>
    public const int DefaultPort = 22;

    /// <summary>Parses <paramref name="text" /> as a jump host.</summary>
    /// <param name="text">The text, such as <c>alice@bastion.example:2222</c>.</param>
    /// <param name="jumpHost">The jump host, or <see langword="null" /> when the text is not one.</param>
    /// <returns><see langword="true" /> when <paramref name="text" /> names a jump host.</returns>
    public static bool TryParse(string text, out SshJumpHost? jumpHost)
    {
        ArgumentNullException.ThrowIfNull(text);
        int at = text.LastIndexOf('@');
        jumpHost = at > 0 && TrySplitHostAndPort(text[(at + 1)..], out string host, out int port)
            ? Create(text[..at], host, port)
            : null;
        return jumpHost is not null;
    }

    /// <summary>Gives the jump host as <c>user@host:port</c>, never with its password.</summary>
    /// <returns>The user, host and port.</returns>
    public override string ToString() =>
        Host.Contains(':', StringComparison.Ordinal) ? $"{User}@[{Host}]:{Port}" : $"{User}@{Host}:{Port}";

    private static SshJumpHost? Create(string userInfo, string host, int port)
    {
        int colon = userInfo.IndexOf(':', StringComparison.Ordinal);
        string user = colon < 0 ? userInfo : userInfo[..colon];
        return user.Length == 0 ? null : new SshJumpHost(user, colon < 0 ? null : userInfo[(colon + 1)..], host, port);
    }

    private static bool TrySplitHostAndPort(string text, out string host, out int port) =>
        text.StartsWith('[') ? TrySplitBracketedHostAndPort(text, out host, out port) : TrySplitNamedHostAndPort(text, out host, out port);

    // [IPv6 address] and an optional port.
    private static bool TrySplitBracketedHostAndPort(string text, out string host, out int port)
    {
        int close = text.IndexOf(']', StringComparison.Ordinal);
        host = close > 1 ? text[1..close] : string.Empty;
        port = DefaultPort;
        return host.Length > 0 && TryReadPort(text[(close + 1)..], out port);
    }

    // A name or IPv4 address and an optional port.
    private static bool TrySplitNamedHostAndPort(string text, out string host, out int port)
    {
        int colon = text.IndexOf(':', StringComparison.Ordinal);
        host = colon < 0 ? text : text[..colon];
        port = DefaultPort;
        return host.Length > 0 && TryReadPort(colon < 0 ? string.Empty : text[colon..], out port);
    }

    // Nothing, for the default port, or ':' and a port from 1 to 65535.
    private static bool TryReadPort(string rest, out int port)
    {
        port = DefaultPort;
        return rest.Length == 0
            || (rest[0] == ':' && int.TryParse(rest.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out port) && port is >= 1 and <= 65535);
    }
}
