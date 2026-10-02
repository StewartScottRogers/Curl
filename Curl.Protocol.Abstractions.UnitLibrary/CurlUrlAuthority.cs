namespace Curl.Protocol.Abstractions;

/// <summary>
/// The authority of a URL, <c>user:password;options@host:port</c>, split and checked as
/// curl 8.21.0's <c>parse_authority</c> does.
/// </summary>
/// <param name="User">The user as written, or <see langword="null" /> when there is no <c>@</c>.</param>
/// <param name="Password">The password as written, or <see langword="null" />.</param>
/// <param name="Options">The login options as written, or <see langword="null" />.</param>
/// <param name="Host">The host as curl holds it; see <see cref="CurlUrl.Host" />.</param>
/// <param name="IdnHost">The host to resolve; see <see cref="CurlUrl.IdnHost" />.</param>
/// <param name="ZoneId">The IPv6 zone id, or <see langword="null" />.</param>
/// <param name="Port">The port written, or <see langword="null" /> when none was.</param>
internal sealed record CurlUrlAuthority(
    string? User,
    string? Password,
    string? Options,
    string Host,
    string IdnHost,
    string? ZoneId,
    int? Port)
{
    /// <summary>The authority of a <c>file</c> URL, which never has a host, a user or a port.</summary>
    public static readonly CurlUrlAuthority None = new(null, null, null, string.Empty, string.Empty, null, null);

    private const int MaximumPort = 65535;

    /// <summary>What <see cref="PortColon" /> returns for a <c>[</c> with no <c>]</c>.</summary>
    private const int UnclosedBracket = -2;

    /// <summary>What <see cref="PortColon" /> returns for a <c>]</c> followed by anything but <c>:</c>.</summary>
    private const int TextAfterBracket = -3;

    /// <summary>
    /// Parses an authority, rejecting what curl rejects.
    /// </summary>
    /// <param name="text">The authority: everything between the slashes after the scheme and the first <c>/</c>, <c>?</c> or <c>#</c>.</param>
    /// <param name="scheme">The scheme written, or <see langword="null" /> when the URL has none yet.</param>
    /// <param name="disallowUser">
    /// <see langword="true" /> to reject user information, even an empty user, before the host
    /// and port are checked, as curl's <c>CURLU_DISALLOW_USER</c> does.
    /// </param>
    /// <param name="rejection">Why curl rejects the authority, or <see cref="CurlUrlRejection.None" />.</param>
    /// <returns>The authority, or <see langword="null" /> when curl rejects it.</returns>
    public static CurlUrlAuthority? Parse(string text, string? scheme, bool disallowUser, out CurlUrlRejection rejection)
    {
        int at = text.IndexOf('@');
        if (disallowUser && at >= 0)
        {
            rejection = CurlUrlRejection.UserNotAllowed;

            return null;
        }

        CurlUrlAuthority login = at < 0
            ? None
            : SplitLogin(text[..at], CurlUrlScheme.HasLoginOptions(scheme));
        string hostAndPort = text[(at + 1)..];

        rejection = SplitPort(hostAndPort, scheme is not null, out string host, out int? port);
        if (rejection == CurlUrlRejection.None && host.Length == 0)
        {
            rejection = CurlUrlRejection.NoHost;
        }

        if (rejection != CurlUrlRejection.None
            || !CurlUrlHost.TryNormalize(host, out string normalizedHost, out string idnHost, out string? zoneId, out rejection))
        {
            return null;
        }

        return login with { Host = normalizedHost, IdnHost = idnHost, ZoneId = zoneId, Port = port };
    }

    /// <summary>
    /// Splits user information as curl's <c>Curl_parse_login_details</c> does: the
    /// password starts at the first <c>:</c>, the options at the first <c>;</c> when the
    /// scheme has them, and each runs to the other separator or the end.
    /// </summary>
    private static CurlUrlAuthority SplitLogin(string login, bool hasLoginOptions)
    {
        int passwordSeparator = login.IndexOf(':');
        int optionsSeparator = hasLoginOptions ? login.IndexOf(';') : -1;

        string user = login[..FirstSeparator(passwordSeparator, optionsSeparator, login.Length)];
        string? password = PartAfter(login, passwordSeparator, optionsSeparator);
        string? options = PartAfter(login, optionsSeparator, passwordSeparator);

        return None with { User = user, Password = password, Options = options };
    }

    private static int FirstSeparator(int first, int second, int length)
    {
        int end = length;
        if (first >= 0)
        {
            end = first;
        }

        if (second >= 0 && second < end)
        {
            end = second;
        }

        return end;
    }

    private static string? PartAfter(string login, int separator, int otherSeparator)
    {
        if (separator < 0)
        {
            return null;
        }

        int end = otherSeparator > separator ? otherSeparator : login.Length;

        return login[(separator + 1)..end];
    }

    /// <summary>
    /// Splits the port from the host as curl's <c>Curl_parse_port</c> does. A colon with
    /// nothing after it is ignored when the URL has a scheme and rejected when it has not.
    /// </summary>
    /// <returns>Why curl rejects the host and port, or <see cref="CurlUrlRejection.None" />.</returns>
    private static CurlUrlRejection SplitPort(string hostAndPort, bool hasScheme, out string host, out int? port)
    {
        host = hostAndPort;
        port = null;

        int colon = PortColon(hostAndPort);
        if (colon == UnclosedBracket)
        {
            return CurlUrlRejection.BadIPv6;
        }

        if (colon == TextAfterBracket)
        {
            return CurlUrlRejection.BadPortNumber;
        }

        if (colon < 0)
        {
            return CurlUrlRejection.None;
        }

        host = hostAndPort[..colon];
        string portText = hostAndPort[(colon + 1)..];
        bool accepted = portText.Length == 0 ? hasScheme : TryParsePort(portText, out port);

        return accepted ? CurlUrlRejection.None : CurlUrlRejection.BadPortNumber;
    }

    /// <summary>
    /// Finds the colon before the port: -1 when there is none,
    /// <see cref="UnclosedBracket" /> when a bracketed IPv6 address is unclosed, and
    /// <see cref="TextAfterBracket" /> when one is followed by anything but a colon.
    /// </summary>
    private static int PortColon(string hostAndPort)
    {
        if (!hostAndPort.StartsWith('['))
        {
            return hostAndPort.IndexOf(':');
        }

        int close = hostAndPort.IndexOf(']');
        if (close < 0)
        {
            return UnclosedBracket;
        }

        int afterClose = close + 1;
        if (afterClose == hostAndPort.Length)
        {
            return -1;
        }

        return hostAndPort[afterClose] == ':' ? afterClose : TextAfterBracket;
    }

    private static bool TryParsePort(string text, out int? port)
    {
        port = null;
        int value = 0;
        foreach (char character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }

            value = (value * 10) + (character - '0');
            if (value > MaximumPort)
            {
                return false;
            }
        }

        port = value;

        return true;
    }
}
