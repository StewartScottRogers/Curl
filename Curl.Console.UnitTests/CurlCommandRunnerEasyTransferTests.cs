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
using Curl.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that a serial transfer performs its attempts through the runner's <see cref="ICurlEngine" />
/// (ADR-0506, ADR-0552): one <c>Libcurl</c> easy handle per transfer (ADR-0624), set with the option group's easy
/// options and <c>CURLOPT_URL</c>, and one <c>curl_easy_perform</c>, so one <see cref="IEasyTransfer.PerformAsync" />,
/// per attempt, <c>--retry</c> attempts included, with the output unchanged; a <c>-Z</c> transfer adds its handle to the
/// run's one multi handle for each attempt (ADR-0626).
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
                curlEngine: engine)
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

        public EasyOptions Options { get => inner.Options; set => inner.Options = value; }

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
