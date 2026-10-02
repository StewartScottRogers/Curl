using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Passes every event on to the transfer's own events and writes the <c>[MULTI]</c> lines curl 8.21.0
/// writes under <c>-v --trace-config multi</c> (or <c>network</c>, <c>all</c>, <c>-vvvv</c>) as its multi
/// state machine takes a transfer from <c>[CONNECT]</c> to <c>[MSGSENT]</c> (measured, BL-1188 Notes).
/// The runner writes <see cref="StartLines" /> itself, as the transfer starts.
/// </summary>
/// <remarks>
/// Curl has no multi state machine, so each group of lines is tied to the line or event of the transfer
/// that curl writes next to it: the connection's <c>[SETUP]</c>, <c>[DNS]</c>, <c>[HAPPY-EYEBALLS]</c> and
/// <c>[TCP]</c> lines, <c>Trying</c>, the connection opened event, <c>using HTTP/</c>, <c>Request
/// completely sent off</c>, the first response header, and the connection's <c>left intact</c> or
/// <c>shutting down connection</c> line. A group whose line never comes is written before the next group
/// that does, so curl's order holds whichever other components are traced. The <c>[PGRS-*] added</c>
/// numbers are the microseconds since the transfer's events were set up, which is what curl's are despite
/// their <c>ns</c>; the poll lines' descriptor is the one the <c>[TCP] connected on fd=</c> line names, or
/// <see cref="DefaultSocketDescriptor" /> when that line is not traced (ADR-0382, BL-1188). A reused
/// connection writes none of the connect groups. A failed connect writes no closing lines; that and a
/// reused connection's own lines are BL-1188's follow-up.
/// </remarks>
internal sealed class MultiStateTraceEvents : ITransferEvents
{
    /// <summary>The lines curl writes as the transfer joins the multi handle, before anything else of it.</summary>
    public static readonly IReadOnlyList<string> StartLines =
    [
        "[MULTI] [INIT] added to multi, mid=1, running=1, total=2",
        "[MULTI] [INIT] pollset[], timeouts=0, paused 0/0 (r/w)",
        "[MULTI] [INIT] multi_wait(fds=0, timeout=0) tinternal=0",
        "[MULTI] [INIT] -> [SETUP]",
        "[MULTI] [SETUP] [PGRS-STARTOP] set",
        "[MULTI] [SETUP] [PGRS-STARTSINGLE] set",
        "[MULTI] [SETUP] -> [CONNECT]",
    ];

    /// <summary>The socket descriptor the poll lines name when no <c>[TCP] connected on fd=</c> line is traced.</summary>
    public const string DefaultSocketDescriptor = "3";

    private const string TcpConnectedPrefix = "[TCP] connected on fd=";
    private const string UsingHttpPrefix = "using HTTP/";
    private const string ConnectionPrefix = "Connection #";
    private const string LeftIntactSuffix = " left intact";
    private const string ShuttingDownPrefix = "shutting down connection #";

    /// <summary>Stands for the first response header among the anchors; no info line can equal it.</summary>
    private const string ResponseHeaderAnchor = "\0response header";

    /// <summary>Stands for the connection opened event, which <c>-v</c> writes as <c>Established connection</c>.</summary>
    private const string ConnectionOpenedAnchor = "\0connection opened";

    /// <summary>Stands for the connection's <c>left intact</c> or <c>shutting down connection</c> line.</summary>
    private const string TransferEndAnchor = "\0transfer end";

    /// <summary>The index of the first group a reused connection still writes: the protocol's, after the connect groups.</summary>
    private const int ProtocolMilestone = 7;

    /// <summary>The lines of a group that writes none on one side of its line.</summary>
    private static readonly Func<string[]> None = Fixed();

    private readonly ITransferEvents inner;
    private readonly TimeProvider timeProvider;
    private readonly Func<long> takeConnectionId;
    private readonly long startTimestamp;
    private readonly Milestone[] milestones;
    private int next;
    private string socketDescriptor = DefaultSocketDescriptor;

    /// <summary>Creates the events, taking the transfer's start time from <paramref name="timeProvider" />.</summary>
    /// <param name="inner">The transfer's own events.</param>
    /// <param name="timeProvider">The clock the <c>[PGRS-*] added</c> numbers are measured on.</param>
    /// <param name="takeConnectionId">Gives the number of the connection the transfer opens.</param>
    public MultiStateTraceEvents(ITransferEvents inner, TimeProvider timeProvider, Func<long> takeConnectionId)
    {
        this.inner = inner;
        this.timeProvider = timeProvider;
        this.takeConnectionId = takeConnectionId;
        startTimestamp = timeProvider.GetTimestamp();
        milestones =
        [
            new(["[SETUP] added", "[DNS] created "], ConnectionCreatedLines),
            new(["[DNS] cf_dns_start"], Fixed("[MULTI] [CONNECT] Curl_conn_setup() -> 0", "[MULTI] [CONNECT] -> [CONNECTING]")),
            new(["[SETUP] happy eyeballing"], NameLookedUpLines),
            new(["[HAPPY-EYEBALLS] init ip ballers"], Fixed("[MULTI] [CONNECTING] cf_setup_connect [0][!DNS][!SETUP][!HAPPY-EYEBALLS]")),
            new(["  Trying "], None),
            new([TcpConnectedPrefix], ConnectPolledLines),
            new([ConnectionOpenedAnchor], ConnectedLines, Fixed("[MULTI] [CONNECTING] connected [0][DNS][SETUP][HAPPY-EYEBALLS][TCP]")),
            new(["[TCP] query ALPN", UsingHttpPrefix], Fixed("[MULTI] [CONNECTING] reduced to [0][TCP]", "[MULTI] [CONNECTING] -> [PROTOCONNECT]", "[MULTI] [PROTOCONNECT] -> [DO]")),
            new([UsingHttpPrefix], None, Fixed("[MULTI] [DO] xfer_setup: recv_idx=0, send_idx=0")),
            new(["Request completely sent off"], None, RequestSentLines),
            new([ResponseHeaderAnchor], ResponsePolledLines, ResponseStartedLines),
            new([ClientWriterTraceEvents.DoneLine, ClientReaderResetTraceEvents.ResetLine, TransferEndAnchor], Fixed("[MULTI] [PERFORMING] -> [DONE]", "[MULTI] [DONE] multi_done: status: 0 prem: 0 done: 0")),
            new([TransferEndAnchor], Fixed("[MULTI] [DONE] multi_done_locked, in use=0"), Fixed("[MULTI] [DONE] -> [COMPLETED]", "[MULTI] [COMPLETED] -> [MSGSENT]", "[MULTI] [COMPLETED] removed from multi, mid=1, running=0, total=1")),
        ];
    }

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        if (StartsWith(text, TcpConnectedPrefix))
        {
            socketDescriptor = text[TcpConnectedPrefix.Length..];
        }

        List<string> after = Advance(EndsTheTransfer(text) ? TransferEndAnchor : text);
        inner.ReportInfo(text);
        Write(after);
    }

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened)
    {
        List<string> after = Advance(ConnectionOpenedAnchor);
        inner.ReportConnectionOpened(opened);
        Write(after);
    }

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused)
    {
        next = Math.Max(next, ProtocolMilestone);
        inner.ReportConnectionReused(reused);
    }

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
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
    {
        List<string> after = Advance(ResponseHeaderAnchor);
        inner.ReportResponseHeader(bytes);
        Write(after);
    }

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => inner.ReportDataSent(bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => inner.ReportDataReceived(bytes);

    /// <summary>
    /// Writes the lines of every group up to the last one <paramref name="text" /> anchors: the whole of
    /// each group passed over, and the lines before <paramref name="text" /> of each it anchors.
    /// </summary>
    /// <param name="text">The line about to be written, or the anchor standing for the event or line about to be written.</param>
    /// <returns>The lines that go after <paramref name="text" />.</returns>
    private List<string> Advance(string text)
    {
        List<string> after = [];
        for (int index = next; index < milestones.Length; index++)
        {
            if (milestones[index].IsAnchoredBy(text))
            {
                for (; next < index; next++)
                {
                    Write(milestones[next].Before());
                    Write(milestones[next].After());
                }

                Write(milestones[index].Before());
                after.AddRange(milestones[index].After());
                next = index + 1;
            }
        }

        return after;
    }

    private void Write(IEnumerable<string> lines)
    {
        foreach (string line in lines)
        {
            inner.ReportInfo(line);
        }
    }

    private string[] ConnectionCreatedLines() =>
    [
        "[MULTI] [CONNECT] transfer credentials: -",
        Invariant($"[MULTI] [CONNECT] [CPOOL] added connection {takeConnectionId()}. The cache now contains 1 members"),
        "[MULTI] [CONNECT] [PGRS-POSTQUEUE] set",
    ];

    private string[] NameLookedUpLines() =>
        [Added("CONNECTING", "NAMELOOKUP"), "[MULTI] [CONNECTING] cf_setup_connect [0][!DNS][!SETUP]"];

    private string[] ConnectPolledLines() =>
    [
        Invariant($"[MULTI] [CONNECTING] pollset[fd={socketDescriptor} OUT], timeouts=0"),
        "[MULTI] [CONNECTING] multi_wait(fds=1, timeout=1000) tinternal=-1",
        "[MULTI] [CONNECTING] cf_setup_connect [0][!DNS][!SETUP][!HAPPY-EYEBALLS]",
    ];

    private string[] ConnectedLines() => [Added("CONNECTING", "CONNECT")];

    private string[] RequestSentLines() =>
        ["[MULTI] [DO] -> [DID]", Added("DID", "PRETRANSFER"), Added("DID", "POSTRANSFER"), "[MULTI] [DID] -> [PERFORMING]"];

    private string[] ResponsePolledLines() =>
    [
        Invariant($"[MULTI] [PERFORMING] pollset[fd={socketDescriptor} IN], timeouts=0"),
        "[MULTI] [PERFORMING] multi_wait(fds=1, timeout=1000) tinternal=-1",
    ];

    private string[] ResponseStartedLines() => [Added("PERFORMING", "STARTTRANSFER")];

    private string Added(string state, string timer) =>
        Invariant($"[MULTI] [{state}] [PGRS-{timer}] added {(long)timeProvider.GetElapsedTime(startTimestamp).TotalMicroseconds}ns");

    private static Func<string[]> Fixed(params string[] lines) => () => lines;

    private static bool StartsWith(string text, string prefix) => text.StartsWith(prefix, StringComparison.Ordinal);

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private static bool EndsTheTransfer(string text) =>
        (StartsWith(text, ConnectionPrefix) && text.EndsWith(LeftIntactSuffix, StringComparison.Ordinal))
        || StartsWith(text, ShuttingDownPrefix);

    /// <summary>One group of lines, tied to the line of the transfer curl writes them beside.</summary>
    /// <param name="Anchors">The starts of the lines, or the anchors standing for events, the group is tied to.</param>
    /// <param name="Before">The group's lines that go before that line.</param>
    /// <param name="After">The group's lines that go after it.</param>
    private sealed record Milestone(string[] Anchors, Func<string[]> Before, Func<string[]> After)
    {
        public Milestone(string[] anchors, Func<string[]> before)
            : this(anchors, before, None)
        {
        }

        public bool IsAnchoredBy(string text) => Array.Exists(Anchors, anchor => StartsWith(text, anchor));
    }
}
