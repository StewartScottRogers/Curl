using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Url;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Authentication;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http;
using Curl.Testing;
using Libcurl.Options;

namespace Curl.Console;

/// <summary>
/// Pins that a serial transfer performs its attempts through the runner's <see cref="ICurlEngine" />
/// (ADR-0506, ADR-0552): one <c>Libcurl</c> easy handle per transfer (ADR-0624), set with the option group's easy
/// options and <c>CURLOPT_URL</c>, and one <c>curl_easy_perform</c>, so one <see cref="IEasyTransfer.PerformAsync" />,
/// per attempt, <c>--retry</c> attempts included, with the output unchanged; a <c>-Z</c> transfer adds its handle to the
/// run's one multi handle for each attempt (ADR-0628).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerEasyTransferTests
{
    private const string Url = "http://127.0.0.1:18241/a";

    private const string Busy = "HTTP/1.1 503 Service Unavailable\r\nContent-Length: 4\r\n\r\nbusy";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();
    private readonly ImmediateTimerTimeProvider clock = new();
    private readonly RecordingCurlEngine engine = new();
    private Func<EasyOptions, IReadOnlyList<EasySetopt>>? easySetoptsOf;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_SerialTransferRetriedTwice_PerformsEachAttemptThroughOneEasyHandle()
    {
        int exitCode = await RunAsync([Busy, Busy, Ok], "--retry", "2", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("easy handles", 1, engine.Handles.Count);
        Diagnostics.Assert("performs", 3, engine.Handles.Sum(handle => handle.Performs));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("busybusyhello", StandardOutputText);
        Assert.AreEqual(1, engine.Handles.Count);
        Assert.AreEqual(3, engine.Handles[0].Performs);
        Assert.AreEqual(Url, engine.Handles[0].Options.Network.Url);
        Assert.IsTrue(engine.Handles[0].Disposed);
    }

    [TestMethod]
    public async Task RunAsync_TwoSerialUrls_PerformEachThroughItsOwnEasyHandle()
    {
        int exitCode = await RunAsync([Ok, Ok], Url, "http://127.0.0.1:18241/b");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hellohello", StandardOutputText);
        CollectionAssert.AreEqual(new[] { 1, 1 }, engine.Handles.Select(handle => handle.Performs).ToArray());
        CollectionAssert.AreEqual(new[] { Url, "http://127.0.0.1:18241/b" }, engine.Handles.Select(handle => handle.Options.Network.Url).ToArray());
    }

    [TestMethod]
    public async Task RunAsync_ParallelTransfersRetried_PerformEachAttemptThroughTheRunsMultiHandle()
    {
        int exitCode = await RunAsync([Busy, Ok, Ok], "-Z", "--parallel-max", "1", "--retry", "1", Url, "http://127.0.0.1:18241/b");

        Diagnostics.Assert("multi handles", 1, engine.MultiHandles);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("busyhellohello", StandardOutputText);
        Assert.AreEqual(1, engine.MultiHandles);
        CollectionAssert.AreEqual(new[] { 2, 1 }, engine.Handles.Select(handle => handle.Performs).ToArray());
        Assert.IsTrue(engine.Handles.TrueForAll(handle => handle.Disposed));
    }

    [TestMethod]
    public async Task RunAsync_ParallelTransferRetriedWithAReferer_WritesOutTheRetrysTransferIdAndTheRefererSent()
    {
        int exitCode = await RunAsync([Busy, Ok], "-Z", "--retry", "1", "-e", "http://r/", "-w", "[%{xfer_id} %{referer} %{scheme}]", Url);

        Diagnostics.Assert("standard output", "busyhello[1 http://r/ http]", StandardOutputText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("busyhello[1 http://r/ http]", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_ParallelTransferRedirectedUnderAutoReferer_WritesOutTheRefererTheLastRequestSent()
    {
        const string Moved = "HTTP/1.1 301 Moved Permanently\r\nLocation: /b\r\nContent-Length: 0\r\n\r\n";

        int exitCode = await RunAsync([Moved, Ok], "-Z", "-L", "-e", ";auto", "-w", "[%{referer}]", Url);

        Diagnostics.Assert("standard output", "hello[" + Url + "]", StandardOutputText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello[" + Url + "]", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_SerialTransferRetriedWithAReferer_WritesOutTheHandlesTransferIdRefererAndScheme()
    {
        int exitCode = await RunAsync([Busy, Ok], "--retry", "1", "-e", "http://r/", "-w", "[%{xfer_id} %{conn_id} %{referer} %{scheme}]", Url);

        Diagnostics.Assert("standard output", "busyhello[1 1 http://r/ http]", StandardOutputText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("busyhello[1 1 http://r/ http]", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_OneOptionOfEachGroup_SetsItThroughSetOptionAndNotOnTheHooks()
    {
        int exitCode = await RunAsync([Ok], "-f", "--max-filesize", "100", "--path-as-is", Url);

        RecordingEasyTransfer handle = engine.Handles[0];
        EasyOptions hooks = handle.OptionsSet[0];
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", StandardOutputText);
        Assert.IsFalse(hooks.Behaviour.FailOnError);
        Assert.AreEqual(0, hooks.Transfer.MaxFileSize);
        Assert.IsFalse(hooks.Network.PathAsIs);
        Assert.IsTrue(handle.Options.Behaviour.FailOnError);
        Assert.AreEqual(100, handle.Options.Transfer.MaxFileSize);
        Assert.IsTrue(handle.Options.Network.PathAsIs);
        Assert.IsGreaterThan(4, handle.OptionsSet.Count);
    }

    [TestMethod]
    public async Task RunAsync_OneOptionOfEachSecurityGroup_SetsItThroughSetOptionAndNotOnTheHooks()
    {
        int exitCode = await RunAsync([Ok], "-k", "-u", "user:secret", "--noproxy", "example", Url);

        RecordingEasyTransfer handle = engine.Handles[0];
        EasyOptions hooks = handle.OptionsSet[0];
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", StandardOutputText);
        Assert.IsTrue(hooks.Tls.Origin.VerifyPeer);
        Assert.IsNull(hooks.Authentication.UserPassword);
        Assert.IsNull(hooks.Proxy.NoProxy);
        Assert.IsFalse(handle.Options.Tls.Origin.VerifyPeer);
        Assert.IsFalse(handle.Options.Tls.Origin.VerifyHost);
        Assert.AreEqual("user:secret", handle.Options.Authentication.UserPassword);
        Assert.AreEqual("example", handle.Options.Proxy.NoProxy);
    }

    [TestMethod]
    public async Task RunAsync_OneOptionOfTheHttpAndProtocolGroupsAndAForm_SetsThemThroughSetOptionAndNotOnTheHooks()
    {
        int exitCode = await RunAsync([Ok], "-A", "agent/1", "--ftp-pret", "-F", "a=b", Url);

        RecordingEasyTransfer handle = engine.Handles[0];
        EasyOptions hooks = handle.OptionsSet[0];
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", StandardOutputText);
        Assert.IsNull(hooks.Http.UserAgent);
        Assert.IsNull(hooks.Http.MimePost);
        Assert.IsFalse(hooks.Protocols.Ftp.UsePret);
        Assert.AreEqual("agent/1", handle.Options.Http.UserAgent);
        Assert.AreEqual("a", handle.Options.Http.MimePost!.Parts[0].Name);
        Assert.AreEqual("b", Encoding.UTF8.GetString(((Api.Options.Http.EasyMimeBytes)handle.Options.Http.MimePost.Parts[0].Data!).Bytes.Span));
        Assert.IsTrue(handle.Options.Protocols.Ftp.UsePret);
    }

    [TestMethod]
    public async Task RunAsync_FormWithAStandardInputPart_KeepsTheFormOnTheHooks()
    {
        int exitCode = await RunAsync([Ok], "-F", "a=@-", Url);

        EasyOptions hooks = engine.Handles[0].OptionsSet[0];
        Assert.AreEqual(0, exitCode);
        Assert.IsInstanceOfType<Api.Options.Http.EasyMimeStandardInput>(hooks.Http.MimePost!.Parts[0].Data);
        Assert.AreSame(hooks.Http.MimePost, engine.Handles[0].Options.Http.MimePost);
    }

    [TestMethod]
    public async Task RunAsync_LimitRateBelowTheSmallestBuffer_KeepsTheReceiveBufferOnTheHooks()
    {
        int exitCode = await RunAsync([Ok], "--limit-rate", "100", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(100, engine.Handles[0].OptionsSet[0].Transfer.BufferSize);
        Assert.AreEqual(100, engine.Handles[0].Options.Transfer.BufferSize);
        Assert.AreEqual(100, engine.Handles[0].Options.Transfer.MaxReceiveSpeed);
    }

    [TestMethod]
    public async Task RunAsync_SetoptLibcurlRefuses_EndsTheRunWithItsCodeBeforeAnyPerform()
    {
        easySetoptsOf = _ => [new EasySetopt(CurlOption.Timecondition, 99L)];

        int exitCode = await RunAsync([Ok, Ok], Url, "http://127.0.0.1:18241/b");

        Assert.AreEqual(43, exitCode);
        Assert.AreEqual(string.Empty, StandardOutputText);
        Assert.Contains($"curl: (43) setopt 0x{(int)CurlOption.Timecondition:x} got bad argument", Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.AreEqual(1, engine.Handles.Count);
        Assert.AreEqual(0, engine.Handles[0].Performs);
    }

    private async Task<int> RunAsync(string[] responses, params string[] arguments)
    {
        ScriptedConnector server = new(responses.Select(Encoding.Latin1.GetBytes));
        HttpProtocolHandler handler = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                outputFiles,
                outputFiles,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                writesProgressMeter: false,
                timeProvider: clock,
                curlEngine: engine,
                easySetoptsOf: easySetoptsOf)
            .RunAsync(arguments);
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout", StandardOutputText);
        Diagnostics.Act("stderr", Encoding.UTF8.GetString(standardError.ToArray()));
        return exitCode;
    }

    /// <summary>A <see cref="ManagedCurlEngine" /> that records every easy handle it creates.</summary>
    private sealed class RecordingCurlEngine : ICurlEngine
    {
        private readonly ManagedCurlEngine inner = new();

        public List<RecordingEasyTransfer> Handles { get; } = [];

        public CurlVersionInfo VersionInfo => inner.VersionInfo;

        public IEasyTransfer CreateEasy()
        {
            RecordingEasyTransfer handle = new((IToolEasyTransfer)inner.CreateEasy());
            Handles.Add(handle);
            return handle;
        }

        public int MultiHandles { get; private set; }

        public IMultiTransfer CreateMulti()
        {
            MultiHandles++;
            return inner.CreateMulti();
        }

        public IShareHandle CreateShare() => inner.CreateShare();

        public ICurlUrlHandle CreateUrl() => inner.CreateUrl();

        public IMimeBuilder CreateMime() => inner.CreateMime();

        public ICurlFormBuilder CreateForm() => inner.CreateForm();

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    /// <summary>An easy handle that counts its performs and passes everything to the managed one.</summary>
    /// <param name="inner">The managed handle.</param>
    private sealed class RecordingEasyTransfer(IToolEasyTransfer inner) : IToolEasyTransfer
    {
        public int Performs { get; private set; }

        public bool Disposed { get; private set; }

        public List<EasyOptions> OptionsSet { get; } = [];

        public EasyOptions Options
        {
            get => inner.Options;
            set
            {
                OptionsSet.Add(value);
                inner.Options = value;
            }
        }

        public TransferInfo Info => inner.Info;

        public string ErrorText => inner.ErrorText;

        public ITransferHeaders Headers => inner.Headers;

        public IEasyWebSocket WebSocket => inner.WebSocket;

        public IConnectOnlyConnection Connection => inner.Connection;

        public EngineTransferSettings? ToolSettings { get => inner.ToolSettings; set => inner.ToolSettings = value; }

        public EasyTransferHost Host { get => inner.Host; set => inner.Host = value; }

        public EngineToolTransferInputs? ToolInputs { get => inner.ToolInputs; set => inner.ToolInputs = value; }

        public EngineToolTransferOutputs? ToolOutputs { get => inner.ToolOutputs; set => inner.ToolOutputs = value; }

        public EngineToolTransferEvents? ToolEvents { get => inner.ToolEvents; set => inner.ToolEvents = value; }

        public EngineToolTransferProgress? ToolProgress { get => inner.ToolProgress; set => inner.ToolProgress = value; }

        public EngineToolTransferDispatch? ToolDispatch { get => inner.ToolDispatch; set => inner.ToolDispatch = value; }

        public TransferResult? LastResult => inner.LastResult;

        public ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
        {
            Performs++;
            return inner.PerformAsync(cancellationToken);
        }

        public IEasyTransfer Duplicate() => inner.Duplicate();

        public void Reset() => inner.Reset();

        public CurlCode Pause(CurlPause directions) => inner.Pause(directions);

        public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => inner.UpkeepAsync(cancellationToken);

        public async ValueTask DisposeAsync()
        {
            Disposed = true;
            await inner.DisposeAsync();
        }
    }
}
