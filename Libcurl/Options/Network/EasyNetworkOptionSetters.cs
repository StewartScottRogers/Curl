using System.Globalization;
using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Libcurl.Url;

namespace Libcurl.Options;

/// <summary>
/// The setter rows of every <c>CURLOPT_*</c> <see cref="EasyNetworkOptions"/> holds, each checking its
/// value as curl 8.22.0's <c>lib/setopt.c</c> does (ADR-0568, ADR-0590).
/// </summary>
internal static class EasyNetworkOptionSetters
{
    /// <summary>The largest count a <c>long</c> option takes before it is cut: libcurl's <c>INT_MAX</c>.</summary>
    private const long MostCount = int.MaxValue;

    /// <summary>The largest TCP port: 65535.</summary>
    private const long MostPort = ushort.MaxValue;

    /// <summary>
    /// The <c>CURLPROTO_*</c> bits of <c>CURLOPT_PROTOCOLS</c> and <c>_REDIR_PROTOCOLS</c>, by bit
    /// number, as their <c>CURLOPT_PROTOCOLS_STR</c> names; <c>ws</c> and <c>wss</c> are bits 30 and 31.
    /// </summary>
    private static readonly string[] ProtocolNames =
    [
        "http", "https", "ftp", "ftps", "scp", "sftp", "telnet", "ldap", "ldaps", "dict", "file",
        "tftp", "imap", "imaps", "pop3", "pop3s", "smtp", "smtps", "rtsp", "rtmp", "rtmpt", "rtmpe",
        "rtmpte", "rtmps", "rtmpts", "gopher", "smb", "smbs", "mqtt", "gophers", "ws", "wss",
    ];

    /// <summary>
    /// Whether this build takes c-aresx27s <c>CURLOPT_DNS_SERVERS</c>, <c>_INTERFACE</c>, <c>_LOCAL_IP4</c> and <c>_LOCAL_IP6</c>: on
    /// everywhere, Curlx27s own resolver taking them where the platformx27s threaded-resolver curl answers
    /// <see cref="CurlCode.UnknownOption"/>, as <c>setopt.c</c> does without <c>USE_RESOLV_ARES</c> (ADR-0995).
    /// </summary>
    internal static bool AresIsBuiltIn { get; set; } = true;

    /// <summary>
    /// Whether this platform's curl takes <c>CURLOPT_TCP_FASTOPEN</c>: Windows has none of <c>CONNECT_DATA_IDEMPOTENT</c>,
    /// <c>MSG_FASTOPEN</c> or <c>TCP_FASTOPEN_CONNECT</c> and answers <see cref="CurlCode.NotBuiltIn"/> (ADR-0995).
    /// </summary>
    internal static bool TcpFastOpenIsBuiltIn { get; set; } = !OperatingSystem.IsWindows();

    /// <summary>Adds the network rows to <paramref name="setters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    internal static void AddTo(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        AddAddressSetters(setters);
        AddResolveSetters(setters);
        AddTcpSetters(setters);
        AddConnectionSetters(setters);
    }

    private static void AddAddressSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.Url] = new(SetUrl);
        setters[CurlOption.Curlu] = new(SetUrlHandle);
        setters[CurlOption.PathAsIs] = new(SetPathAsIs);
        setters[CurlOption.Protocols] = new(SetProtocolBits);
        setters[CurlOption.ProtocolsStr] = new(SetProtocols);
        setters[CurlOption.RedirProtocols] = new(SetRedirectProtocolBits);
        setters[CurlOption.RedirProtocolsStr] = new(SetRedirectProtocols);
        setters[CurlOption.DefaultProtocol] = new(SetDefaultProtocol);
        setters[CurlOption.Port] = new(SetPort);
        setters[CurlOption.Interface] = new(SetInterface);
        setters[CurlOption.Localport] = new(SetLocalPort);
        setters[CurlOption.Localportrange] = new(SetLocalPortRange);
        setters[CurlOption.UnixSocketPath] = new(SetUnixSocketPath);
        setters[CurlOption.AbstractUnixSocket] = new(SetAbstractUnixSocket);
        setters[CurlOption.AddressScope] = new(SetAddressScope);
    }

    private static void AddResolveSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.ConnectTo] = new(SetConnectTo);
        setters[CurlOption.Resolve] = new(SetResolve);
        setters[CurlOption.DnsCacheTimeout] = new(SetDnsCacheTimeout);
        setters[CurlOption.DnsUseGlobalCache] = new(IgnoreDnsUseGlobalCache);
        setters[CurlOption.DnsServers] = new(SetDnsServers);
        setters[CurlOption.DnsInterface] = new(SetDnsInterface);
        setters[CurlOption.DnsLocalIp4] = new(SetDnsLocalIPv4);
        setters[CurlOption.DnsLocalIp6] = new(SetDnsLocalIPv6);
        setters[CurlOption.DnsShuffleAddresses] = new(SetDnsShuffleAddresses);
        setters[CurlOption.DohUrl] = new(SetDohUrl);
        setters[CurlOption.Ipresolve] = new(SetIpResolve);
    }

    private static void AddTcpSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.TcpNodelay] = new(SetTcpNoDelay);
        setters[CurlOption.TcpFastopen] = new(SetTcpFastOpen);
        setters[CurlOption.TcpKeepalive] = new(SetTcpKeepAlive);
        setters[CurlOption.TcpKeepidle] = new(SetTcpKeepIdle);
        setters[CurlOption.TcpKeepintvl] = new(SetTcpKeepInterval);
        setters[CurlOption.TcpKeepcnt] = new(SetTcpKeepCount);
    }

    private static void AddConnectionSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.Connecttimeout] = new(SetConnectTimeoutSeconds);
        setters[CurlOption.ConnecttimeoutMs] = new(SetConnectTimeoutMilliseconds);
        setters[CurlOption.HappyEyeballsTimeoutMs] = new(SetHappyEyeballsTimeout);
        setters[CurlOption.FreshConnect] = new(SetFreshConnect);
        setters[CurlOption.ForbidReuse] = new(SetForbidReuse);
        setters[CurlOption.Maxconnects] = new(SetMaxConnects);
        setters[CurlOption.MaxageConn] = new(SetMaxIdleConnectionAge);
        setters[CurlOption.MaxlifetimeConn] = new(SetMaxConnectionLifetime);
        setters[CurlOption.ConnectOnly] = new(SetConnectOnly);
        setters[CurlOption.AccepttimeoutMs] = new(SetAcceptTimeout);
        setters[CurlOption.UpkeepIntervalMs] = new(SetUpkeepInterval);
        setters[CurlOption.ServerResponseTimeout] = new(SetServerResponseTimeoutSeconds);
        setters[CurlOption.ServerResponseTimeoutMs] = new(SetServerResponseTimeoutMilliseconds);
        setters[CurlOption.UseSsl] = new(SetUseSsl);
    }

    private static CurlCode SetUrl(ref EasyOptions o, object? v) => Set(ref o, n => n with { Url = (string?)v });

    /// <summary>
    /// <c>CURLOPT_CURLU</c>: a <see cref="CurlUrlHandle"/>'s engine handle, held by reference as libcurl
    /// holds the pointer; <see langword="null"/> clears it, and any other object is
    /// <see cref="CurlCode.BadFunctionArgument"/> (ADR-0590).
    /// </summary>
    private static CurlCode SetUrlHandle(ref EasyOptions o, object? v) => v switch
    {
        null => Set(ref o, n => n with { UrlHandle = null }),
        CurlUrlHandle url => Set(ref o, n => n with { UrlHandle = url.EngineHandle }),
        _ => CurlCode.BadFunctionArgument,
    };

    private static CurlCode SetPathAsIs(ref EasyOptions o, object? v) => Set(ref o, n => n with { PathAsIs = IsOn(v) });

    /// <summary><c>CURLOPT_PROTOCOLS</c>: the <c>CURLPROTO_*</c> bitmask is stored as its <c>_STR</c> name list (<see cref="ProtocolsOf"/>), the one form the options hold.</summary>
    private static CurlCode SetProtocolBits(ref EasyOptions o, object? v) => Set(ref o, n => n with { Protocols = ProtocolsOf(NumberOf(v)) });

    /// <summary><c>CURLOPT_REDIR_PROTOCOLS</c>: stored as its <c>_STR</c> name list, as <see cref="SetProtocolBits"/> does.</summary>
    private static CurlCode SetRedirectProtocolBits(ref EasyOptions o, object? v) => Set(ref o, n => n with { RedirectProtocols = ProtocolsOf(NumberOf(v)) });

    /// <summary><c>CURLOPT_PROTOCOLS_STR</c>: the list is kept as given once <see cref="CheckProtocolNames"/> accepts it.</summary>
    private static CurlCode SetProtocols(ref EasyOptions o, object? v)
    {
        var check = CheckProtocolNames(v);
        var names = (string?)v;
        return check == CurlCode.Ok ? Set(ref o, n => n with { Protocols = names }) : check;
    }

    /// <summary><c>CURLOPT_REDIR_PROTOCOLS_STR</c>: checked and kept as <see cref="SetProtocols"/> does, for redirects only.</summary>
    private static CurlCode SetRedirectProtocols(ref EasyOptions o, object? v)
    {
        var check = CheckProtocolNames(v);
        var names = (string?)v;
        return check == CurlCode.Ok ? Set(ref o, n => n with { RedirectProtocols = names }) : check;
    }

    private static CurlCode SetDefaultProtocol(ref EasyOptions o, object? v) => Set(ref o, n => n with { DefaultProtocol = (string?)v });

    private static CurlCode SetPort(ref EasyOptions o, object? v) => SetInRange(ref o, v, 0, MostPort, (n, port) => n with { Port = (int)port });

    private static CurlCode SetInterface(ref EasyOptions o, object? v) => HasEmptyInterfaceName((string?)v) ? CurlCode.BadFunctionArgument : Set(ref o, n => n with { Interface = (string?)v });

    /// <summary>True for <c>if!</c>, <c>host!</c> and <c>ifhost!</c> with nothing after the bang, which libcurl refuses.</summary>
    private static bool HasEmptyInterfaceName(string? value) => value is "if!" or "host!" or "ifhost!";

    private static CurlCode SetLocalPort(ref EasyOptions o, object? v) => SetInRange(ref o, v, 0, MostPort, (n, port) => n with { LocalPort = (int)port });

    private static CurlCode SetLocalPortRange(ref EasyOptions o, object? v) => SetInRange(ref o, v, 0, MostPort, (n, range) => n with { LocalPortRange = (int)range });

    private static CurlCode SetUnixSocketPath(ref EasyOptions o, object? v) => Set(ref o, n => n with { UnixSocketPath = (string?)v });

    private static CurlCode SetAbstractUnixSocket(ref EasyOptions o, object? v) => Set(ref o, n => n with { AbstractUnixSocket = (string?)v });

    private static CurlCode SetAddressScope(ref EasyOptions o, object? v) => SetInRange(ref o, v, int.MinValue, uint.MaxValue, (n, scope) => n with { AddressScope = unchecked((uint)scope) });

    private static CurlCode SetConnectTo(ref EasyOptions o, object? v) => Set(ref o, n => n with { ConnectTo = ListOf(v) });

    private static CurlCode SetResolve(ref EasyOptions o, object? v) => Set(ref o, n => n with { Resolve = ListOf(v) });

    /// <summary><c>CURLOPT_DNS_USE_GLOBAL_CACHE</c>: deprecated, and ignored with <see cref="CurlCode.Ok"/>, as libcurl does.</summary>
    private static CurlCode IgnoreDnsUseGlobalCache(ref EasyOptions o, object? v) => CurlCode.Ok;

    /// <summary><c>CURLOPT_DNS_SERVERS</c>: the servers, or <see cref="CurlCode.UnknownOption"/> where <see cref="AresIsBuiltIn"/> is off.</summary>
    private static CurlCode SetDnsServers(ref EasyOptions o, object? v) => !AresIsBuiltIn ? CurlCode.UnknownOption : Set(ref o, n => n with { DnsServers = (string?)v });

    /// <summary><c>CURLOPT_DNS_INTERFACE</c>: the interface, or <see cref="CurlCode.UnknownOption"/> where <see cref="AresIsBuiltIn"/> is off.</summary>
    private static CurlCode SetDnsInterface(ref EasyOptions o, object? v) => !AresIsBuiltIn ? CurlCode.UnknownOption : Set(ref o, n => n with { DnsInterface = (string?)v });

    /// <summary><c>CURLOPT_DNS_LOCAL_IP4</c>: the address, or <see cref="CurlCode.UnknownOption"/> where <see cref="AresIsBuiltIn"/> is off.</summary>
    private static CurlCode SetDnsLocalIPv4(ref EasyOptions o, object? v) => !AresIsBuiltIn ? CurlCode.UnknownOption : Set(ref o, n => n with { DnsLocalIPv4 = (string?)v });

    /// <summary><c>CURLOPT_DNS_LOCAL_IP6</c>: the address, or <see cref="CurlCode.UnknownOption"/> where <see cref="AresIsBuiltIn"/> is off.</summary>
    private static CurlCode SetDnsLocalIPv6(ref EasyOptions o, object? v) => !AresIsBuiltIn ? CurlCode.UnknownOption : Set(ref o, n => n with { DnsLocalIPv6 = (string?)v });

    private static CurlCode SetDnsShuffleAddresses(ref EasyOptions o, object? v) => Set(ref o, n => n with { DnsShuffleAddresses = IsOn(v) });

    private static CurlCode SetDohUrl(ref EasyOptions o, object? v) => Set(ref o, n => n with { DohUrl = (string?)v });

    private static CurlCode SetIpResolve(ref EasyOptions o, object? v) =>
        SetInRange(ref o, v, (long)IpResolve.Whatever, (long)IpResolve.V6, (n, version) => n with { IpResolve = (IpResolve)version });

    private static CurlCode SetTcpNoDelay(ref EasyOptions o, object? v) => Set(ref o, n => n with { TcpNoDelay = IsOn(v) });

    /// <summary><c>CURLOPT_TCP_FASTOPEN</c>: on for non-zero, or <see cref="CurlCode.NotBuiltIn"/> where <see cref="TcpFastOpenIsBuiltIn"/> is off.</summary>
    private static CurlCode SetTcpFastOpen(ref EasyOptions o, object? v) => !TcpFastOpenIsBuiltIn ? CurlCode.NotBuiltIn : Set(ref o, n => n with { TcpFastOpen = IsOn(v) });

    private static CurlCode SetTcpKeepAlive(ref EasyOptions o, object? v) => Set(ref o, n => n with { TcpKeepAlive = IsOn(v) });

    private static CurlCode SetTcpKeepIdle(ref EasyOptions o, object? v) => SetCount(ref o, v, (n, seconds) => n with { TcpKeepIdle = TimeSpan.FromSeconds(seconds) });

    private static CurlCode SetTcpKeepInterval(ref EasyOptions o, object? v) => SetCount(ref o, v, (n, seconds) => n with { TcpKeepInterval = TimeSpan.FromSeconds(seconds) });

    private static CurlCode SetTcpKeepCount(ref EasyOptions o, object? v) => SetCount(ref o, v, (n, count) => n with { TcpKeepCount = (int)count });

    private static CurlCode SetConnectTimeoutSeconds(ref EasyOptions o, object? v) => SetCount(ref o, v, (n, seconds) => n with { ConnectTimeout = TimeSpan.FromSeconds(seconds) });

    private static CurlCode SetConnectTimeoutMilliseconds(ref EasyOptions o, object? v) => SetCount(ref o, v, (n, ms) => n with { ConnectTimeout = TimeSpan.FromMilliseconds(ms) });

    private static CurlCode SetHappyEyeballsTimeout(ref EasyOptions o, object? v) => SetCount(ref o, v, (n, ms) => n with { HappyEyeballsTimeout = TimeSpan.FromMilliseconds(ms) });

    private static CurlCode SetFreshConnect(ref EasyOptions o, object? v) => Set(ref o, n => n with { FreshConnect = IsOn(v) });

    private static CurlCode SetForbidReuse(ref EasyOptions o, object? v) => Set(ref o, n => n with { ForbidReuse = IsOn(v) });

    /// <summary><c>CURLOPT_MAXCONNECTS</c>: 0 to <c>INT_MAX</c>; negative values are <see cref="CurlCode.BadFunctionArgument"/>.</summary>
    private static CurlCode SetMaxConnects(ref EasyOptions o, object? v) => SetInRange(ref o, v, 0, MostCount, (n, count) => n with { MaxConnects = (int)count });

    private static CurlCode SetMaxIdleConnectionAge(ref EasyOptions o, object? v) => SetCount(ref o, v, (n, seconds) => n with { MaxIdleConnectionAge = TimeSpan.FromSeconds(seconds) });

    private static CurlCode SetMaxConnectionLifetime(ref EasyOptions o, object? v) => SetCount(ref o, v, (n, seconds) => n with { MaxConnectionLifetime = TimeSpan.FromSeconds(seconds) });

    private static CurlCode SetConnectOnly(ref EasyOptions o, object? v) =>
        SetInRange(ref o, v, (long)ConnectOnly.Disabled, (long)ConnectOnly.WebSocket, (n, mode) => n with { ConnectOnly = (ConnectOnly)mode });

    private static CurlCode SetAcceptTimeout(ref EasyOptions o, object? v) => SetCount(ref o, v, (n, ms) => n with { AcceptTimeout = TimeSpan.FromMilliseconds(ms) });

    private static CurlCode SetUpkeepInterval(ref EasyOptions o, object? v) => SetCount(ref o, v, (n, ms) => n with { UpkeepInterval = TimeSpan.FromMilliseconds(ms) });

    private static CurlCode SetServerResponseTimeoutSeconds(ref EasyOptions o, object? v) => SetCount(ref o, v, (n, seconds) => n with { ServerResponseTimeout = TimeSpan.FromSeconds(seconds) });

    private static CurlCode SetServerResponseTimeoutMilliseconds(ref EasyOptions o, object? v) => SetCount(ref o, v, (n, ms) => n with { ServerResponseTimeout = TimeSpan.FromMilliseconds(ms) });

    private static CurlCode SetUseSsl(ref EasyOptions o, object? v) =>
        SetInRange(ref o, v, (long)UseSsl.None, (long)UseSsl.All, (n, level) => n with { UseSsl = (UseSsl)level });

    /// <summary>
    /// <c>CURLOPT_DNS_CACHE_TIMEOUT</c>: seconds, -1 for forever (<see cref="Timeout.InfiniteTimeSpan"/>);
    /// below -1 is <see cref="CurlCode.BadFunctionArgument"/>, and past <c>INT_MAX</c> is cut to it.
    /// </summary>
    private static CurlCode SetDnsCacheTimeout(ref EasyOptions options, object? value)
    {
        var seconds = NumberOf(value);
        return seconds < -1
            ? CurlCode.BadFunctionArgument
            : Set(ref options, n => n with { DnsCacheTimeout = seconds == -1 ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(Math.Min(seconds, MostCount)) });
    }

    /// <summary>Replaces the handle's <see cref="EasyNetworkOptions"/> with the copy <paramref name="change"/> returns; always <see cref="CurlCode.Ok"/>.</summary>
    private static CurlCode Set(ref EasyOptions options, Func<EasyNetworkOptions, EasyNetworkOptions> change)
    {
        options = options with { Network = change(options.Network) };
        return CurlCode.Ok;
    }

    /// <summary>A count or time from 0, cut to <c>INT_MAX</c>; a negative value is <see cref="CurlCode.BadFunctionArgument"/>.</summary>
    private static CurlCode SetCount(ref EasyOptions options, object? value, Func<EasyNetworkOptions, long, EasyNetworkOptions> change)
    {
        var number = NumberOf(value);
        return number < 0 ? CurlCode.BadFunctionArgument : Set(ref options, n => change(n, Math.Min(number, MostCount)));
    }

    /// <summary>A value from <paramref name="least"/> to <paramref name="most"/>; any other is <see cref="CurlCode.BadFunctionArgument"/>.</summary>
    private static CurlCode SetInRange(ref EasyOptions options, object? value, long least, long most, Func<EasyNetworkOptions, long, EasyNetworkOptions> change)
    {
        var number = NumberOf(value);
        return number < least || number > most ? CurlCode.BadFunctionArgument : Set(ref options, n => change(n, number));
    }

    /// <summary>
    /// <c>CURLOPT_PROTOCOLS_STR</c>'s check (<c>protocol2num</c>): <see langword="null"/> (back to the default)
    /// and <c>all</c> are <see cref="CurlCode.Ok"/>, as is a comma-separated list naming at least one known
    /// scheme in any letter case (empty entries are skipped); a list naming any unknown scheme is
    /// <see cref="CurlCode.UnsupportedProtocol"/>, and one naming nothing at all (<c>""</c>, <c>","</c>) is
    /// <see cref="CurlCode.BadFunctionArgument"/>.
    /// </summary>
    private static CurlCode CheckProtocolNames(object? value)
    {
        if (value is not string names || string.Equals(names, "all", StringComparison.OrdinalIgnoreCase))
        {
            return CurlCode.Ok;
        }

        var listed = names.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (listed.Length == 0)
        {
            return CurlCode.BadFunctionArgument;
        }

        return listed.All(IsSchemeName)
            ? CurlCode.Ok
            : CurlCode.UnsupportedProtocol;
    }

    /// <summary>
    /// Whether <paramref name="name"/> is a scheme libcurl has a handler for: a <c>CURLPROTO_*</c> name or
    /// <c>mqtts</c>, which shares <c>mqtt</c>'s bit.
    /// </summary>
    private static bool IsSchemeName(string name) =>
        string.Equals(name, "mqtts", StringComparison.OrdinalIgnoreCase)
        || ProtocolNames.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The <c>CURLOPT_PROTOCOLS_STR</c> list for a <c>CURLPROTO_*</c> bitmask: <c>all</c> when every bit
    /// is set (<c>CURLPROTO_ALL</c>), else the set bits' names, comma-separated.
    /// </summary>
    private static string ProtocolsOf(long bits)
    {
        var names = ProtocolNames.Where((_, bit) => (bits & (1L << bit)) != 0).ToArray();
        return names.Length == ProtocolNames.Length ? "all" : string.Join(',', names);
    }

    /// <summary>A private copy of a string list option (<c>curl_slist</c>), so a later change by the caller does not alter the handle; <see langword="null"/> is an empty list.</summary>
    private static IReadOnlyList<string> ListOf(object? value) => value is IEnumerable<string> lines ? [.. lines] : [];

    /// <summary>Whether a <c>CURLOT_LONG</c> on/off value is on: any value but 0, as in libcurl.</summary>
    private static bool IsOn(object? value) => NumberOf(value) != 0;

    private static long NumberOf(object? value) => Convert.ToInt64(value, CultureInfo.InvariantCulture);
}
