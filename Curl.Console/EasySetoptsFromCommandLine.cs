using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;
using Libcurl.Options;

namespace Curl.Console;

/// <summary>
/// The <c>curl_easy_setopt</c> calls the curl tool's <c>config2setopts.c</c> makes for a serial transfer's
/// behaviour, transfer, network (BL-2491, ADR-0631), TLS, authentication and proxy options (BL-2492, ADR-0639): one <see cref="EasySetopt" /> per
/// <c>CURLOPT_*</c> whose value the command line moved off libcurl's default. What libcurl has no
/// <c>CURLOPT_*</c> for, or what its setter would clamp to another value than the command line's, stays on
/// the engine transfer's hooks (<see cref="KeptOnHooks" />).
/// </summary>
internal static partial class EasySetoptsFromCommandLine
{
    // libcurl's INT_MAX, past which its setters cut a time or a count.
    private const long MostCount = int.MaxValue;

    // CURLOPT_BUFFERSIZE's READBUFFER_MIN and READBUFFER_MAX; the setter raises or cuts anything outside.
    private const long LeastBufferSize = 1024;

    private const long MostBufferSize = 10 * 1024 * 1024;

    private static readonly EasyBehaviourOptions UnsetBehaviour = new();

    private static readonly EasyTransferOptions UnsetTransfer = new();

    private static readonly EasyNetworkOptions UnsetNetwork = new();

    private static readonly SetoptRow<EasyBehaviourOptions>[] BehaviourRows =
    [
        new(CurlOption.Verbose, b => FlagOf(b.Verbose)),
        new(CurlOption.Header, b => FlagOf(b.IncludeHeader)),
        new(CurlOption.Noprogress, b => FlagOf(b.NoProgress)),
        new(CurlOption.Failonerror, b => FlagOf(b.FailOnError)),
        new(CurlOption.NewFilePerms, b => (long)b.NewFilePermissions),
    ];

    private static readonly SetoptRow<EasyTransferOptions>[] TransferRows =
    [
        new(CurlOption.Range, t => t.Range),
        new(CurlOption.ResumeFromLarge, t => t.ResumeFrom),
        new(CurlOption.Customrequest, t => t.CustomRequest),
        new(CurlOption.Filetime, t => FlagOf(t.FileTime)),
        new(CurlOption.Dirlistonly, t => FlagOf(t.DirListOnly)),
        new(CurlOption.Nobody, t => FlagOf(t.NoBody)),
        new(CurlOption.Upload, t => FlagOf(t.Upload)),
        new(CurlOption.InfilesizeLarge, t => t.InFileSize),
        new(CurlOption.MaxfilesizeLarge, t => t.MaxFileSize),
        new(CurlOption.Timecondition, t => (long)t.TimeCondition),
        new(CurlOption.TimevalueLarge, t => t.TimeValue),
        new(CurlOption.Transfertext, t => FlagOf(t.TransferText)),
        new(CurlOption.Crlf, t => FlagOf(t.Crlf)),
        new(CurlOption.LowSpeedLimit, t => t.LowSpeedLimit),
        new(CurlOption.LowSpeedTime, t => WholeSecondsOf(t.LowSpeedTime), t => WholeSecondsOf(t.LowSpeedTime) is not null),
        new(CurlOption.MaxSendSpeedLarge, t => t.MaxSendSpeed),
        new(CurlOption.MaxRecvSpeedLarge, t => t.MaxReceiveSpeed),
        new(CurlOption.Buffersize, t => t.BufferSize, IsSettableBufferSize),
        new(CurlOption.TimeoutMs, t => WholeMillisecondsOf(t.Timeout), t => WholeMillisecondsOf(t.Timeout) is not null),
    ];

    private static readonly SetoptRow<EasyNetworkOptions>[] NetworkRows =
    [
        new(CurlOption.PathAsIs, n => FlagOf(n.PathAsIs)),
        new(CurlOption.ProtocolsStr, n => n.Protocols),
        new(CurlOption.RedirProtocolsStr, n => n.RedirectProtocols),
        new(CurlOption.DefaultProtocol, n => n.DefaultProtocol),
        new(CurlOption.Interface, n => n.Interface),
        new(CurlOption.Localport, n => (long)n.LocalPort),
        new(CurlOption.Localportrange, n => (long)n.LocalPortRange),
        new(CurlOption.ConnectTo, n => ListOf(n.ConnectTo)),
        new(CurlOption.Resolve, n => ListOf(n.Resolve)),
        new(CurlOption.DnsServers, n => n.DnsServers),
        new(CurlOption.DnsInterface, n => n.DnsInterface),
        new(CurlOption.DnsLocalIp4, n => n.DnsLocalIPv4),
        new(CurlOption.DnsLocalIp6, n => n.DnsLocalIPv6),
        new(CurlOption.DohUrl, n => n.DohUrl),
        new(CurlOption.Ipresolve, n => (long)n.IpResolve),
        new(CurlOption.TcpNodelay, n => FlagOf(n.TcpNoDelay)),
        new(CurlOption.TcpFastopen, n => FlagOf(n.TcpFastOpen)),
        new(CurlOption.TcpKeepalive, n => FlagOf(n.TcpKeepAlive)),
        new(CurlOption.TcpKeepidle, n => WholeSecondsOf(n.TcpKeepIdle), n => WholeSecondsOf(n.TcpKeepIdle) is not null),
        new(CurlOption.TcpKeepintvl, n => WholeSecondsOf(n.TcpKeepInterval), n => WholeSecondsOf(n.TcpKeepInterval) is not null),
        new(CurlOption.TcpKeepcnt, n => (long)n.TcpKeepCount),
        new(CurlOption.UnixSocketPath, n => n.UnixSocketPath),
        new(CurlOption.AbstractUnixSocket, n => n.AbstractUnixSocket),
        new(CurlOption.ConnecttimeoutMs, n => WholeMillisecondsOf(n.ConnectTimeout), n => WholeMillisecondsOf(n.ConnectTimeout) is not null),
        new(CurlOption.HappyEyeballsTimeoutMs, n => WholeMillisecondsOf(n.HappyEyeballsTimeout), n => WholeMillisecondsOf(n.HappyEyeballsTimeout) is not null),
        new(CurlOption.UseSsl, n => (long)n.UseSsl),
    ];

    /// <summary>
    /// The <c>curl_easy_setopt</c> calls for <paramref name="options" />' behaviour, transfer, network, TLS (origin,
    /// proxy, then the handle's own), authentication and proxy groups: one per field with a <c>CURLOPT_*</c> whose value
    /// differs from libcurl's default, in the groups' order.
    /// </summary>
    /// <param name="options">The transfer's easy options, as <see cref="EasyOptionsFromCommandLine.EasyOptionsOf" /> maps them.</param>
    /// <returns>The calls.</returns>
    internal static IReadOnlyList<EasySetopt> Of(EasyOptions options) =>
    [
        .. SetoptsOf(BehaviourRows, options.Behaviour, UnsetBehaviour),
        .. SetoptsOf(TransferRows, options.Transfer, UnsetTransfer),
        .. SetoptsOf(NetworkRows, options.Network, UnsetNetwork),
        .. TlsAuthenticationAndProxySetoptsOf(options),
    ];

    /// <summary>
    /// What the engine transfer's hooks keep of <paramref name="options" />: every other group whole, and of the
    /// groups <see cref="Of" /> sets only what it does not - libcurl's defaults, with
    /// <c>--ip-tos</c>, <c>--vlan-priority</c> and <c>--mptcp</c> (no <c>CURLOPT_*</c>; curl's tool sets them in its
    /// socket callbacks), a receive buffer, <c>-y</c>, <c>-m</c>, <c>--connect-timeout</c>,
    /// <c>--happy-eyeballs-timeout-ms</c> or <c>--keepalive-time</c> its setter would hold to another value,
    /// <c>--engine</c>, an <c>--ech</c> value <c>CURLOPT_ECH</c> would ignore, the TLS-SRP fields and Curl's
    /// <c>--httpsig-*</c> options (ADR-0639).
    /// </summary>
    /// <param name="options">The transfer's easy options.</param>
    /// <returns>The options for the hooks.</returns>
    internal static EasyOptions KeptOnHooks(EasyOptions options) =>
        options with
        {
            Behaviour = UnsetBehaviour,
            Transfer = TransferKeptOnHooks(options.Transfer),
            Network = NetworkKeptOnHooks(options.Network),
            Tls = TlsKeptOnHooks(options.Tls),
            Authentication = AuthenticationKeptOnHooks(options.Authentication),
            Proxy = UnsetProxy,
        };

    private static EasyTransferOptions TransferKeptOnHooks(EasyTransferOptions transfer) =>
        UnsetTransfer with
        {
            BufferSize = IsSettableBufferSize(transfer) ? UnsetTransfer.BufferSize : transfer.BufferSize,
            LowSpeedTime = KeptUnlessWhole(transfer.LowSpeedTime, TimeSpan.TicksPerSecond, UnsetTransfer.LowSpeedTime),
            Timeout = KeptUnlessWhole(transfer.Timeout, TimeSpan.TicksPerMillisecond, UnsetTransfer.Timeout),
        };

    private static EasyNetworkOptions NetworkKeptOnHooks(EasyNetworkOptions network) =>
        UnsetNetwork with
        {
            IpTypeOfService = network.IpTypeOfService,
            VlanPriority = network.VlanPriority,
            MultipathTcp = network.MultipathTcp,
            TcpKeepIdle = KeptUnlessWhole(network.TcpKeepIdle, TimeSpan.TicksPerSecond, UnsetNetwork.TcpKeepIdle),
            TcpKeepInterval = KeptUnlessWhole(network.TcpKeepInterval, TimeSpan.TicksPerSecond, UnsetNetwork.TcpKeepInterval),
            ConnectTimeout = KeptUnlessWhole(network.ConnectTimeout, TimeSpan.TicksPerMillisecond, UnsetNetwork.ConnectTimeout),
            HappyEyeballsTimeout = KeptUnlessWhole(network.HappyEyeballsTimeout, TimeSpan.TicksPerMillisecond, UnsetNetwork.HappyEyeballsTimeout),
        };

    private static IEnumerable<EasySetopt> SetoptsOf<T>(SetoptRow<T>[] rows, T given, T unset) =>
        rows.Where(row => row.IsSettable(given) && !Equals(row.ValueOf(given), row.ValueOf(unset)))
            .Select(row => new EasySetopt(row.Option, row.ValueOf(given)));

    // A time the setter takes in whole units stays on the hooks when it is not one: it would be cut.
    private static TimeSpan KeptUnlessWhole(TimeSpan time, long ticksPerUnit, TimeSpan unset) =>
        WholeUnitsOf(time, ticksPerUnit) is null ? time : unset;

    private static bool IsSettableBufferSize(EasyTransferOptions transfer) =>
        transfer.BufferSize is >= LeastBufferSize and <= MostBufferSize;

    private static long? WholeSecondsOf(TimeSpan time) => WholeUnitsOf(time, TimeSpan.TicksPerSecond);

    private static long? WholeMillisecondsOf(TimeSpan time) => WholeUnitsOf(time, TimeSpan.TicksPerMillisecond);

    // The time in whole units up to INT_MAX, or null when the setter would hold it to another value.
    private static long? WholeUnitsOf(TimeSpan time, long ticksPerUnit) =>
        time.Ticks % ticksPerUnit == 0 && time.Ticks / ticksPerUnit is >= 0 and <= MostCount ? time.Ticks / ticksPerUnit : null;

    private static long FlagOf(bool on) => on ? 1L : 0L;

    // An empty list is libcurl's default, no slist; a list is passed as the strings it holds.
    private static string[]? ListOf(IReadOnlyList<string> lines) => lines.Count == 0 ? null : [.. lines];

    /// <summary>One <c>CURLOPT_*</c> of an option group: the value it is set to and whether its setter keeps it as given.</summary>
    /// <typeparam name="T">The option group's record.</typeparam>
    /// <param name="Option">The option.</param>
    /// <param name="ValueOf">The value the group's record sets it to.</param>
    /// <param name="Settable">Whether the setter keeps that value as given; <see langword="null" /> when it always does.</param>
    private sealed record SetoptRow<T>(CurlOption Option, Func<T, object?> ValueOf, Func<T, bool>? Settable = null)
    {
        public bool IsSettable(T given) => Settable?.Invoke(given) ?? true;
    }
}
