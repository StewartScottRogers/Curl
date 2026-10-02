using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Passes every event on to <paramref name="inner" /> and adds the <c>[HAPPY-EYEBALLS]</c> and
/// <c>[TCP]</c> lines curl 8.21.0 writes around its connect attempts under <c>--trace-config
/// happy-eyeballs</c>, <c>tcp</c>, <c>network</c> or <c>all</c> (measured, BL-1161 Notes), and the
/// <c>[TIMER] [HAPPY_EYEBALLS]</c> lines of its second-family timer under <c>timer</c>, <c>network</c> or
/// <c>all</c> (measured, BL-1186 Notes). It sits below
/// the <c>[DNS]</c> and <c>[SETUP]</c> filters' events, so the lines it writes as a <c>Trying</c> line
/// passes through come before theirs; the <see cref="AddressFamilyRace" /> tells it the rest
/// (<see cref="RaceStarting" />, <see cref="SecondFamilyDue" />, <see cref="AttemptFailing" />,
/// <see cref="AttemptFailed" />, <see cref="AttemptConnected" />, <see cref="NoMoreAttempts" />).
/// </summary>
/// <remarks>
/// curl's volatile values are produced as ADR-0357 records: socket descriptors are numbered from
/// <see cref="FirstSocketDescriptor" /> per connect, the local address written as a socket opens is
/// its family's unspecified address and port <c>0</c>, as .NET binds the local end only as it
/// connects (ADR-0100), and each wake-up of the race writes one poll round, where curl repeats its
/// <c>not connected yet</c> and <c>adjust_pollset</c> lines once per poll of the system.
/// </remarks>
/// <param name="inner">The transfer's own events.</param>
/// <param name="host">The host the connection dials, after any <c>--connect-to</c> mapping.</param>
/// <param name="tracesHappyEyeballs">Whether the <c>[HAPPY-EYEBALLS]</c> lines are written.</param>
/// <param name="tracesTcp">Whether the <c>[TCP]</c> lines are written.</param>
/// <param name="tracesTimer">Whether the <c>[TIMER] [HAPPY_EYEBALLS]</c> lines are written.</param>
internal sealed class ConnectAttemptTraceEvents(ITransferEvents inner, string host, bool tracesHappyEyeballs, bool tracesTcp, bool tracesTimer = false) : ITransferEvents
{
    /// <summary>The descriptor the first socket of a connect is written with.</summary>
    public const int FirstSocketDescriptor = 3;

    /// <summary>The line the filter writes as it is removed after <c>Established connection</c>.</summary>
    public const string RemovingLine = "[HAPPY-EYEBALLS] removing connected setup filter";

    /// <summary>The line the filter writes as it is destroyed, after <see cref="RemovingLine" />.</summary>
    public const string DestroyLine = "[HAPPY-EYEBALLS] destroy";

    private const string TryingPrefix = "  Trying ";
    private const string TryingSuffix = "...";

    private readonly List<(IPEndPoint RemoteEndPoint, int Descriptor, int Number)> _ongoing = [];
    private int _attempts;
    private TimeSpan? _secondFamilyTimeout;

    /// <summary>Gets a value indicating whether the <c>[HAPPY-EYEBALLS]</c> lines are written.</summary>
    public bool TracesHappyEyeballs => tracesHappyEyeballs;

    /// <summary>
    /// Notes that the race has a second family, started after <paramref name="timeout" />, which
    /// curl names after the first attempt's <c>checked connect attempts</c> line.
    /// </summary>
    /// <param name="timeout">The <c>--happy-eyeballs-timeout-ms</c> delay.</param>
    public void RaceStarting(TimeSpan timeout) => _secondFamilyTimeout = timeout;

    /// <summary>Writes the poll round in which the second family's delay ran out.</summary>
    public void SecondFamilyDue()
    {
        WritePollRound();
        foreach (var attempt in _ongoing)
        {
            WriteTcp($"not connected yet on fd={attempt.Descriptor}");
        }

        WriteChecked();
        WriteHappyEyeballs("happy eyeballs timeout expired, start next attempt");
    }

    /// <summary>
    /// Writes the poll round in which the attempt at <paramref name="remoteEndPoint" /> failed, before
    /// the race's <c>connect to ... failed</c> line.
    /// </summary>
    /// <param name="remoteEndPoint">Where the attempt went.</param>
    public void AttemptFailing(IPEndPoint remoteEndPoint)
    {
        WritePollRound();
        WriteTcp($"poll/select error on fd={Find(remoteEndPoint).Descriptor}");
    }

    /// <summary>
    /// Writes the failed attempt's socket closing, after the race's <c>connect to ... failed</c> line.
    /// </summary>
    /// <param name="remoteEndPoint">Where the attempt went.</param>
    public void AttemptFailed(IPEndPoint remoteEndPoint)
    {
        _ongoing.Remove(Find(remoteEndPoint));
        WriteTcp("destroy");
        WriteChecked();
    }

    /// <summary>
    /// Writes the poll round in which the attempt at <paramref name="remoteEndPoint" /> connected,
    /// the closing of every other attempt still running, and the filter's <c>Connected to</c> line.
    /// </summary>
    /// <param name="remoteEndPoint">Where the winning attempt went.</param>
    public void AttemptConnected(IPEndPoint remoteEndPoint)
    {
        WritePollRound();
        var winner = Find(remoteEndPoint);
        foreach (var attempt in _ongoing)
        {
            WriteTcp(attempt == winner ? $"connected on fd={attempt.Descriptor}" : $"not connected yet on fd={attempt.Descriptor}");
        }

        WriteHappyEyeballs($"connect attempt #{winner.Number} successful");
        CloseLosers(winner);
        WriteTimer("cleared");
        WriteHappyEyeballs($"Connected to {host} ({remoteEndPoint.Address}) port {remoteEndPoint.Port}");
    }

    private void CloseLosers((IPEndPoint RemoteEndPoint, int Descriptor, int Number) winner)
    {
        foreach (var attempt in _ongoing.Where(attempt => attempt != winner))
        {
            WriteTcp("destroy");
            WriteTcp($"cf_socket_close, fd={attempt.Descriptor}");
        }

        _ongoing.Clear();
    }

    /// <summary>Writes the lines with which the filter gives up once every address has failed.</summary>
    public void NoMoreAttempts()
    {
        WriteHappyEyeballs("want to do more");
        WriteHappyEyeballs("check for next AAAA address: none");
        WriteHappyEyeballs("check for next A address: none");
        WriteHappyEyeballs("no more attempts to try");
        WriteHappyEyeballs("baller 0: result=7");
    }

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        if (!text.StartsWith(TryingPrefix, StringComparison.Ordinal))
        {
            inner.ReportInfo(text);
            return;
        }

        var remoteEndPoint = IPEndPoint.Parse(text[TryingPrefix.Length..^TryingSuffix.Length]);
        WriteAttemptStarting(remoteEndPoint.AddressFamily == AddressFamily.InterNetworkV6);
        inner.ReportInfo(text);
        WriteSocketOpened(remoteEndPoint);
    }

    // curl's ballers look for an IPv6 address first; a later attempt looks only in its own family.
    private void WriteAttemptStarting(bool isIPv6)
    {
        var (recordType, version) = isIPv6 ? ("AAAA", "ipv6") : ("A", "ipv4");
        if (_attempts == 0)
        {
            WriteHappyEyeballs("init ip ballers for transport 3");
            WriteHappyEyeballs("want to do more");
            if (!isIPv6)
            {
                WriteHappyEyeballs("check for next AAAA address: none");
            }

            WriteHappyEyeballs($"check for next {recordType} address: found");
            WriteHappyEyeballs($"starting first attempt for {version} -> 0");
            return;
        }

        WriteHappyEyeballs("want to do more");
        WriteHappyEyeballs($"check for next {recordType} address: found");
        WriteHappyEyeballs($"starting next attempt for {version} -> 0");
        foreach (var attempt in _ongoing)
        {
            WriteTcp($"not connected yet on fd={attempt.Descriptor}");
        }
    }

    private void WriteSocketOpened(IPEndPoint remoteEndPoint)
    {
        var descriptor = FirstSocketDescriptor + _attempts;
        _ongoing.Add((remoteEndPoint, descriptor, _attempts));
        var unspecified = remoteEndPoint.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;
        WriteTcp($"Set TCP_KEEP* on fd={descriptor}");
        WriteTcp($"cf_socket_open() -> 0, fd={descriptor}");
        WriteTcp($"local address {unspecified} port 0...");
        WriteChecked();
        if (_attempts == 0 && _secondFamilyTimeout is { } timeout)
        {
            WriteHappyEyeballs($"next HAPPY_EYEBALLS timeout in {(long)timeout.TotalMilliseconds}ms");
            WriteTimer($"set for {(long)timeout.TotalMicroseconds}ns");
            WriteTimer($"gives multi timeout in {(long)timeout.TotalMilliseconds}ms");
        }

        _attempts++;
    }

    private void WritePollRound()
    {
        foreach (var attempt in _ongoing)
        {
            WriteTcp($"adjust_pollset, !connected, POLLOUT fd={attempt.Descriptor}");
        }

        WriteHappyEyeballs($"adjust_pollset -> 0, {_ongoing.Count} socks");
    }

    private void WriteChecked() => WriteHappyEyeballs($"checked connect attempts: {_ongoing.Count} ongoing, 0 inconclusive");

    private (IPEndPoint RemoteEndPoint, int Descriptor, int Number) Find(IPEndPoint remoteEndPoint) =>
        _ongoing.First(attempt => attempt.RemoteEndPoint.Equals(remoteEndPoint));

    private void WriteHappyEyeballs(string text)
    {
        if (tracesHappyEyeballs)
        {
            inner.ReportInfo($"[HAPPY-EYEBALLS] {text}");
        }
    }

    // curl names its timers in microseconds followed by "ns" (measured, BL-1186 Notes).
    private void WriteTimer(string text)
    {
        if (tracesTimer)
        {
            inner.ReportInfo($"[TIMER] [HAPPY_EYEBALLS] {text}");
        }
    }

    private void WriteTcp(string text)
    {
        if (tracesTcp)
        {
            inner.ReportInfo($"[TCP] {text}");
        }
    }

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened) => inner.ReportConnectionOpened(opened);

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused) => inner.ReportConnectionReused(reused);

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake) => inner.ReportTlsHandshake(handshake);

    /// <inheritdoc />
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => inner.ReportTlsData(bytes, sent);

    /// <inheritdoc />
    public void ReportTlsMessage(TlsMessageEvent message) => inner.ReportTlsMessage(message);

    /// <inheritdoc />
    public void ReportTlsTrust(TlsTrustEvent trust) => inner.ReportTlsTrust(trust);

    /// <inheritdoc />
    public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) =>
        inner.ReportCertificateVerifyResult(verifyResult, isProxy);

    /// <inheritdoc />
    public void ReportTlsEarlyData(long bytes) => inner.ReportTlsEarlyData(bytes);

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => inner.ReportRequestHeader(bytes);

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => inner.ReportResponseHeader(bytes);

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => inner.ReportDataSent(bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => inner.ReportDataReceived(bytes);
}
