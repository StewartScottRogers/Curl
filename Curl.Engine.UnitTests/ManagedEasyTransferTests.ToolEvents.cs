using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins <see cref="ManagedEasyTransfer.ToolEvents" /> (BL-2397, ADR-0561): a perform from the options with the curl
/// tool's events set reports the same event sequence, byte for byte, as the same attempt the runner's own context
/// gives when performed directly (<see cref="TransferPerformer.FollowWatchingSpeedAsync" />), reports none to the debug callback, and takes the
/// <c>%{conn_id}</c> the runner's recorder kept on its state without numbering one itself.
/// </summary>
public sealed partial class ManagedEasyTransferTests
{
    private const string EventsResponse = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";

    [TestMethod]
    public async Task PerformAsync_ToolEventsSet_ReportsTheEventSequenceTheRunnersContextReports()
    {
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = "http://h/x" } };
        EngineTransferState fromOptionsState = new();
        RecordingToolEvents fromOptions = new(fromOptionsState, 7);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(new ScriptedConnector([Encoding.ASCII.GetBytes(EventsResponse)])).CreateEasy();
        easy.Options = options;
        easy.ToolEvents = new EngineToolTransferEvents(fromOptions, fromOptionsState, () => -2);
        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        EngineTransferState attemptState = new();
        RecordingToolEvents fromAttempt = new(attemptState, 7);
        await TransferPerformer.FollowWatchingSpeedAsync(
            attemptState,
            new RedirectFollower(CreateDispatcher(new ScriptedConnector([Encoding.ASCII.GetBytes(EventsResponse)]), null)),
            new TransferContextFactory(Stream.Null, TimeProvider.System).Create(
                ManagedEasyTransfer.TransferSettingsOf(options),
                ManagedEasyTransfer.WithoutDefaultUserAgent(options).Http,
                options.Behaviour,
                options.Transfer,
                options.Protocols,
                options.Proxy,
                options.Authentication,
                options.Network,
                CurlUrl.Parse("http://h/x"),
                Stream.Null,
                range: null,
                resumeFrom: null,
                headerOutput: null,
                events: fromAttempt),
            new RedirectPolicy());

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.IsTrue(fromOptions.Log.Any(entry => entry.StartsWith("header-out:GET /x HTTP/1.1\r\n", StringComparison.Ordinal)));
        Assert.Contains("header-in:HTTP/1.1 200 OK\r\n", fromOptions.Log);
        CollectionAssert.AreEqual(fromAttempt.Log.ToArray(), fromOptions.Log.ToArray());
        Assert.AreEqual(attemptState.ConnectionId, easy.Info.ConnectionId);
    }

    [TestMethod]
    public async Task PerformAsync_ToolEventsSet_TakesTheRecordersConnectionIdAndNumbersNoneItself()
    {
        await using ManagedCurlEngine engine = CreateEngine(new ScriptedConnector([Encoding.ASCII.GetBytes(EventsResponse), Encoding.ASCII.GetBytes(EventsResponse)]));
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();
        int debugCalls = 0;
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Behaviour = new EasyBehaviourOptions { Verbose = true },
            Callbacks = new() { Debug = (_, _) => ++debugCalls },
        };
        EngineTransferState state = new();
        easy.ToolEvents = new EngineToolTransferEvents(new RecordingToolEvents(state, 7), state, () => -2);

        await easy.PerformAsync(CancellationToken.None);
        long toolConnectionId = easy.Info.ConnectionId;
        await using ManagedEasyTransfer next = (ManagedEasyTransfer)engine.CreateEasy();
        next.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h2/x" } };
        await next.PerformAsync(CancellationToken.None);

        Assert.AreEqual(7, toolConnectionId);
        Assert.AreEqual(7, state.ConnectionId);
        Assert.AreEqual(0, debugCalls);
        Assert.AreEqual(0, next.Info.ConnectionId);
    }

    [TestMethod]
    public async Task Duplicate_HandleWithToolEvents_DoesNotCopyThemAndResetClearsThem()
    {
        await using ManagedEasyTransfer easy = EasyOver(RecordingProtocolHandler.WritingPath("http"));
        easy.ToolEvents = new EngineToolTransferEvents(NoTransferEvents.Instance, new EngineTransferState(), () => -2);

        await using IEasyTransfer copy = easy.Duplicate();
        easy.Reset();

        Assert.IsNull(((ManagedEasyTransfer)copy).ToolEvents);
        Assert.IsNull(easy.ToolEvents);
    }

    [TestMethod]
    public async Task PerformAsync_ToolEventsSet_InfoAnswersTheRunnersIdsSchemeRefererAndTlsResults()
    {
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(new ScriptedConnector([Encoding.ASCII.GetBytes(EventsResponse)])).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "HTTP://h/x" },
            Http = new EasyHttpOptions { Referer = "http://r/" },
        };
        EngineTransferState state = new()
        {
            TransferId = 5,
            RequestUrl = "http://h/x?q=1",
            SslVerifyResult = 18,
            ProxySslVerifyResult = 20,
            TlsEarlyDataSent = -3,
        };
        int takes = 0;
        easy.ToolEvents = new EngineToolTransferEvents(NoTransferEvents.Instance, state, () => 9 + takes++);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(5, easy.Info.TransferId);
        Assert.AreEqual(9, easy.Info.ConnectionId);
        Assert.AreEqual(9, state.ConnectionId);
        Assert.AreEqual(1, takes);
        Assert.AreEqual("http", easy.Info.Scheme);
        Assert.AreEqual("http://r/", easy.Info.Referer);
        Assert.AreEqual(18, easy.Info.SslVerifyResult);
        Assert.AreEqual(20, easy.Info.ProxySslVerifyResult);
        Assert.AreEqual(-3, easy.Info.EarlyDataSent);
    }

    [TestMethod]
    public async Task PerformAsync_ToolEventsSetForAnUnsupportedScheme_AnswersTheRetrysTransferIdNoConnectionAndNoScheme()
    {
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(new ScriptedConnector([])).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "xyz://a/b" } };
        EngineTransferState state = new() { TransferId = 1, RetryTransferId = 3, RequestUrl = "xyz://a/b" };
        easy.ToolEvents = new EngineToolTransferEvents(NoTransferEvents.Instance, state, () => 9);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.UnsupportedProtocol, code);
        Assert.AreEqual(3, easy.Info.TransferId);
        Assert.AreEqual(ManagedEasyTransfer.NoConnectionId, easy.Info.ConnectionId);
        Assert.IsNull(state.ConnectionId);
        Assert.IsNull(easy.Info.Scheme);
    }

    [TestMethod]
    public async Task PerformAsync_ToolEventsSetAndAReadCallbackAborts_TakesTheRunnersNextConnectionId()
    {
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(new ScriptedConnector([Encoding.ASCII.GetBytes(Continue + Response)])).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/up" },
            Transfer = new EasyTransferOptions { Upload = true },
            Callbacks = new() { Read = _ => ReadCallbackResult.Abort },
        };
        EngineTransferState state = new() { TransferId = 2 };
        easy.ToolEvents = new EngineToolTransferEvents(NoTransferEvents.Instance, state, () => 4);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.AbortedByCallback, code);
        Assert.AreEqual(2, easy.Info.TransferId);
        Assert.AreEqual(4, easy.Info.ConnectionId);
    }

    // Logs every event the runner's -v and --trace writers would see, its bytes as Latin-1, and numbers a
    // connection on the state at the first request line as the runner's ConnectionIdRecordingTransferEvents does.
    private sealed class RecordingToolEvents(EngineTransferState state, long connectionId) : ITransferEvents
    {
        public List<string> Log { get; } = [];

        public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Log.Add("opened");

        public void ReportConnectionReused(ConnectionReusedEvent reused) => Log.Add("reused");

        public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => Log.Add("verify:" + verifyResult);

        public void ReportTlsEarlyData(long bytes) => Log.Add("early:" + bytes);

        public void ReportInfo(string text) => Log.Add("info:" + text);

        public void ReportTlsHandshake(TlsHandshakeEvent handshake) => Log.Add("handshake");

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => Log.Add("tls:" + Encoding.Latin1.GetString(bytes));

        public void ReportTlsMessage(TlsMessageEvent message) => Log.Add("tls-message");

        public void ReportTlsTrust(TlsTrustEvent trust) => Log.Add("trust");

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
        {
            state.ConnectionId ??= connectionId;
            Log.Add("header-out:" + Encoding.Latin1.GetString(bytes));
        }

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Log.Add("header-in:" + Encoding.Latin1.GetString(bytes));

        public void ReportDataSent(ReadOnlySpan<byte> bytes) => Log.Add("data-out:" + Encoding.Latin1.GetString(bytes));

        public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Log.Add("data-in:" + Encoding.Latin1.GetString(bytes));
    }
}
