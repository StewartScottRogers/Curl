using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Pins the <c>-v</c> lines curl 8.21.0 writes for an <c>Alt-Svc</c> alternative or a
/// <c>Strict-Transport-Security</c> header its caches will not take, each just before the header
/// line that caused it (measured 2026-10-03, ADR-0409), driven through <see cref="ScriptedAltSvcStore" />
/// and <see cref="ScriptedHstsStore" />.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string SkipHttpsUrl = "https://h.test:18499/a";

    private const string SkipHttpUrl = "http://h.test:18499/a";

    [TestMethod]
    [DataRow(AltSvcSkipReason.BadHostname, "* Bad alt-svc hostname, ignoring.")]
    [DataRow(AltSvcSkipReason.BadIpv6Hostname, "* Bad alt-svc IPv6 hostname, ignoring.")]
    [DataRow(AltSvcSkipReason.UnknownPortNumber, "* Unknown alt-svc port number, ignoring.")]
    public async Task ExecuteAsync_HttpsAltSvcTheStoreSkips_ReportsTheReasonJustBeforeTheHeaderLine(AltSvcSkipReason reason, string expected)
    {
        RecordingTransferEvents events = await SkipExchangeAsync(
            SkipHttpsUrl,
            "Alt-Svc: h2=\":abc\"",
            new HttpRequestOptions { AltSvcStore = new ScriptedAltSvcStore([AltSvcHeaderOutcome.Skipping(reason)]) });

        AssertJustBefore(events, expected, "< Alt-Svc: h2=\":abc\"\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpsAltSvcAddedThenSkipped_ReportsBothInHeaderOrderBeforeTheHeaderLine()
    {
        AltSvcHeaderOutcome[] outcomes = [AltSvcHeaderOutcome.Adding(new AltSvcAlternative("h2", "h.test", 8443)), AltSvcHeaderOutcome.Skipping(AltSvcSkipReason.UnknownPortNumber)];

        RecordingTransferEvents events = await SkipExchangeAsync(
            SkipHttpsUrl,
            "Alt-Svc: h2=\":8443\", h2=\"host:\"",
            new HttpRequestOptions { AltSvcStore = new ScriptedAltSvcStore(outcomes) });

        int header = events.Events.IndexOf("< Alt-Svc: h2=\":8443\", h2=\"host:\"\r\n");
        CollectionAssert.AreEqual(
            new[] { "* Added alt-svc: h.test:8443 over h2", "* Unknown alt-svc port number, ignoring." },
            events.Events[(header - 2)..header]);
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpAltSvc_WritesNoSkipLine()
    {
        ScriptedAltSvcStore store = new([AltSvcHeaderOutcome.Skipping(AltSvcSkipReason.UnknownPortNumber)]);

        RecordingTransferEvents events = await SkipExchangeAsync(SkipHttpUrl, "Alt-Svc: h2=\":abc\"", new HttpRequestOptions { AltSvcStore = store });

        Assert.IsEmpty(store.Responses);
        Assert.IsFalse(events.Events.Any(line => line.Contains("alt-svc", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpsStsTheStoreRefuses_ReportsIllegalStsHeaderSkippedJustBeforeTheHeaderLine()
    {
        ScriptedHstsStore store = new(legal: false);

        RecordingTransferEvents events = await SkipExchangeAsync(SkipHttpsUrl, "Strict-Transport-Security: max-age=abc", new HttpRequestOptions { HstsStore = store });

        AssertJustBefore(events, "* Illegal STS header skipped", "< Strict-Transport-Security: max-age=abc\r\n");
        Assert.AreEqual((CurlUrl.Parse(SkipHttpsUrl), "max-age=abc", CookieTime), store.Responses.Single());
    }

    /// <summary>
    /// The store answers <see langword="true" /> for a legal header and for an IP-address host,
    /// whose illegal header curl skips without a line (ADR-0409).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_HttpsStsTheStoreTakes_WritesNoLine()
    {
        ScriptedHstsStore store = new(legal: true);

        RecordingTransferEvents events = await SkipExchangeAsync("https://127.0.0.1:18499/a", "strict-transport-security: max-age=abc", new HttpRequestOptions { HstsStore = store });

        Assert.HasCount(1, store.Responses);
        Assert.IsFalse(events.Events.Any(line => line.Contains("Illegal STS", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpSts_DoesNotCallTheStore()
    {
        ScriptedHstsStore store = new(legal: false);

        RecordingTransferEvents events = await SkipExchangeAsync(SkipHttpUrl, "Strict-Transport-Security: max-age=abc", new HttpRequestOptions { HstsStore = store });

        Assert.IsEmpty(store.Responses);
        Assert.IsFalse(events.Events.Any(line => line.Contains("Illegal STS", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpsStsWithoutAStore_Succeeds()
    {
        RecordingTransferEvents events = await SkipExchangeAsync(SkipHttpsUrl, "Strict-Transport-Security: max-age=abc", null);

        Assert.IsFalse(events.Events.Any(line => line.Contains("Illegal STS", StringComparison.Ordinal)));
    }

    /// <summary>Runs one exchange whose 200 response carries <paramref name="header" />, and gives its events.</summary>
    private async Task<RecordingTransferEvents> SkipExchangeAsync(string url, string header, HttpRequestOptions? options)
    {
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("url", url);
        Diagnostics.Arrange("scripted response", $"200, {header}, Content-Length: 0");
        Diagnostics.Arrange("stores", $"alt-svc {(options?.AltSvcStore is null ? "none" : "scripted")}, hsts {(options?.HstsStore is null ? "none" : "scripted")}");

        TransferResult result = await Handler(QueueConnector.For(Connection($"HTTP/1.1 200 OK\r\n{header}\r\nContent-Length: 0\r\n\r\n", 65536)))
            .ExecuteAsync(CookieContext(url, options, events));

        WriteResult(result);
        WriteEvents("events", events.Events);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        return events;
    }

    private void AssertJustBefore(RecordingTransferEvents events, string line, string headerLine)
    {
        int at = events.Events.IndexOf(line);
        Diagnostics.Assert("line just before the header line", OneLine(line + " | " + headerLine), at < 0 ? "(line missing)" : OneLine(string.Join(" | ", events.Events.Skip(at).Take(2))));
        Assert.IsGreaterThanOrEqualTo(0, at, $"No '{line}' in {string.Join(" | ", events.Events)}");
        Assert.AreEqual(headerLine, events.Events[at + 1]);
    }
}
