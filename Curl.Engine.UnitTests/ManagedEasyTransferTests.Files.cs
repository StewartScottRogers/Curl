using System.Net;
using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Cookies;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins the runner's per-transfer setup and cleanup done through the easy handle (BL-2362, ADR-0544):
/// the <c>-b</c> files loaded and the <c>-c</c> jar written as <see cref="TransferPerformer" />'s steps do
/// them for the runner, the <c>--alt-svc</c> and <c>--hsts</c> files read before and written after the
/// transfer, and <c>%{conn_id}</c> numbered across pooled connections.
/// </summary>
public sealed partial class ManagedEasyTransferTests
{
    private const string CookieFileText = "example.com\tFALSE\t/\tFALSE\t0\tsent\t1\n";

    private const string SetsCookie = "HTTP/1.1 200 OK\r\nSet-Cookie: got=2\r\nContent-Length: 0\r\n\r\n";

    [TestMethod]
    public async Task PerformAsync_CookieFileAndJar_SendsTheFilesCookieAndWritesTheJarAsThePerformersStepsDo()
    {
        EasyHttpOptions http = new() { CookieFiles = ["in.txt"], CookieJar = "out.txt" };
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = "http://example.com/x" }, Http = http };
        ScriptedConnector easyConnector = new([Encoding.ASCII.GetBytes(SetsCookie)]);
        MemoryFileSystem easyFiles = new() { Files = { ["in.txt"] = Encoding.ASCII.GetBytes(CookieFileText) } };
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(easyConnector).CreateEasy();
        easy.Options = options;
        easy.Host = HostOf(easyFiles);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);
        (byte[] toolWritten, byte[] toolJar) = await PerformWithThePerformersCookieStepsAsync(options);

        Assert.AreEqual(CurlCode.Ok, code);
        StringAssert.Contains(Encoding.ASCII.GetString(easyConnector.Written), "Cookie: sent=1\r\n");
        CollectionAssert.AreEqual(toolWritten, easyConnector.Written);
        CollectionAssert.AreEqual(toolJar, easyFiles.Files["out.txt"]);
        StringAssert.Contains(Encoding.ASCII.GetString(easyFiles.Files["out.txt"]), "\tgot\t2");
    }

    [TestMethod]
    public async Task PerformAsync_AltSvcAndHstsFiles_ReadsThemBeforeAndWritesThemAfterTheTransfer()
    {
        const string LearnsBoth = "HTTP/1.1 200 OK\r\nAlt-Svc: h2=\"alt:443\"; ma=3600\r\nStrict-Transport-Security: max-age=31536000\r\nContent-Length: 0\r\n\r\n";
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(LearnsBoth)]);
        MemoryFileSystem files = new() { Files = { ["hsts.txt"] = Encoding.ASCII.GetBytes("h \"20991231 00:00:00\"\n") } };
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Http = new EasyHttpOptions { AltSvc = "altsvc.txt", Hsts = "hsts.txt" },
        };
        easy.Host = HostOf(files);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        CollectionAssert.AreEqual(new[] { "read altsvc.txt", "read hsts.txt", "write altsvc.txt", "write hsts.txt" }, files.Opened);
        Assert.AreEqual(443, connector.Targets.Single().Port);
        StringAssert.Contains(Encoding.ASCII.GetString(files.Files["altsvc.txt"]), "h2 alt 443");
        StringAssert.Contains(Encoding.ASCII.GetString(files.Files["hsts.txt"]), "h \"");
    }

    [TestMethod]
    public async Task PerformAsync_JarToStandardOutputOnTheLibraryHost_PrintsItNowhere()
    {
        await using IEasyTransfer easy = CreateEngine(new ScriptedConnector([Encoding.ASCII.GetBytes(SetsCookie)])).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Http = new EasyHttpOptions { CookieJar = "-" },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreSame(EasyTransferHost.Library, ((ManagedEasyTransfer)easy).Host);
    }

    [TestMethod]
    public async Task PerformAsync_TwoTransfersOnOnePooledConnectionThenANewOne_NumbersConnIdByConnection()
    {
        const string KeepsAlive = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";
        const string Closes = "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 0\r\n\r\n";
        OpenReportingConnector connector = new(new ScriptedConnector([Encoding.ASCII.GetBytes(KeepsAlive), Encoding.ASCII.GetBytes(Closes), Encoding.ASCII.GetBytes(KeepsAlive)]));
        await using ManagedCurlEngine engine = new(TimeProvider.System, (_, shared, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
            new PoolingConnector(connector, shared.Connections, configuration: null),
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            cookies)));
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" } };
        List<long> connectionIds = [];

        for (int transfer = 0; transfer < 3; transfer++)
        {
            Assert.AreEqual(CurlCode.Ok, await easy.PerformAsync(CancellationToken.None));
            connectionIds.Add(easy.Info.ConnectionId);
        }

        CollectionAssert.AreEqual(new long[] { 0, 0, 1 }, connectionIds);
        Assert.AreEqual(2, connector.Opened);
    }

    [TestMethod]
    public async Task PerformAsync_RedirectOpeningASecondConnection_KeepsTheFirstConnectionsConnId()
    {
        const string Redirects = "HTTP/1.1 302 Found\r\nLocation: http://other/y\r\nConnection: close\r\nContent-Length: 0\r\n\r\n";
        OpenReportingConnector connector = new(new ScriptedConnector([Encoding.ASCII.GetBytes(Redirects), Encoding.ASCII.GetBytes(Response)]));
        await using IEasyTransfer easy = new ManagedCurlEngine(TimeProvider.System, (_, _, cookies) => CreateDispatcher(connector, cookies)).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Http = new EasyHttpOptions { FollowLocation = FollowLocation.All },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(2, connector.Opened);
        Assert.AreEqual(0, easy.Info.ConnectionId);
    }

    [TestMethod]
    public async Task PerformAsync_SchemeNoHandlerServes_HasNoConnId()
    {
        await using IEasyTransfer easy = CreateEngine(new ScriptedConnector([])).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "nosuch://h/x" } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.UnsupportedProtocol, code);
        Assert.AreEqual(ManagedEasyTransfer.NoConnectionId, easy.Info.ConnectionId);
    }

    [TestMethod]
    public async Task Duplicate_HandleWithAHost_CopiesTheHostAndResetKeepsIt()
    {
        EasyTransferHost host = HostOf(new MemoryFileSystem());
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(new ScriptedConnector([])).CreateEasy();
        easy.Host = host;

        await using IEasyTransfer copy = easy.Duplicate();
        easy.Reset();

        Assert.AreSame(host, ((ManagedEasyTransfer)copy).Host);
        Assert.AreSame(host, easy.Host);
    }

    // What the runner does today around its serial attempt for -b and -c (CurlCommandRunner): the
    // performer's LoadCookieFilesAsync before it and WriteCookieJarAltSvcAndHstsFilesAsync after it.
    private static async Task<(byte[] Written, byte[] Jar)> PerformWithThePerformersCookieStepsAsync(EasyOptions handleOptions)
    {
        // The tool sets CURLOPT_USERAGENT itself; the handle's options without one send none (BL-2559).
        EasyOptions options = ManagedEasyTransfer.WithoutDefaultUserAgent(handleOptions);
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(SetsCookie)]);
        MemoryFileSystem files = new() { Files = { ["in.txt"] = Encoding.ASCII.GetBytes(CookieFileText) } };
        TransferPerformer performer = new(TimeProvider.System, _ => string.Empty);
        EngineTransferSettings settings = ManagedEasyTransfer.TransferSettingsOf(options);
        CookieEngine cookies = CookieEngine.FromHttpOptions(options.Http, new CookieStore())!;
        EngineTransferState state = new();
        Core.Hsts.HstsTransferPolicy hsts = new(TimeProvider.System);
        await performer.LoadCookieFilesAsync(cookies, settings, "http://example.com/x", files, Stream.Null, NoTransferEvents.Instance);
        TransferContext context = new TransferContextFactory(Stream.Null, TimeProvider.System) { HstsStore = hsts }.Create(
            settings, options.Http, options.Behaviour, options.Transfer, options.Protocols, options.Proxy, options.Authentication, options.Network, CurlUrl.Parse("http://example.com/x"), Stream.Null, range: null, resumeFrom: null, headerOutput: null);

        await TransferPerformer.FollowWatchingSpeedAsync(state, new RedirectFollower(CreateDispatcher(connector, cookies.HandlerStore)), context, RedirectPolicyMapping.Of(settings, options.Http, options.Authentication));
        await performer.WriteCookieJarAltSvcAndHstsFilesAsync(cookies, settings, options.Http, "http://example.com/x", () => Stream.Null, state, hsts, files, runsOnWindows: false);

        return (connector.Written, files.Files["out.txt"]);
    }

    private static EasyTransferHost HostOf(MemoryFileSystem files) =>
        new(files, Stream.Null, () => Stream.Null, NoDiagnosticLog.Instance, RunsOnWindows: false, AltSvcUsesOverHttp: false);

    /// <summary>Keeps files in memory: a read opens what <see cref="Files" /> holds, a write replaces it when the stream is disposed, and every open is recorded in order.</summary>
    private sealed class MemoryFileSystem : IFileSystem
    {
        public Dictionary<string, byte[]> Files { get; } = [];

        public List<string> Opened { get; } = [];

        public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken)
        {
            Opened.Add("read " + path);
            return ValueTask.FromResult(Files.TryGetValue(path, out byte[]? content)
                ? FileOpenResult.Opened(new MemoryStream(content, writable: false), content.Length, null)
                : FileOpenResult.Failed(FileAccessStatus.NotFound));
        }

        public ValueTask<FileOpenResult> OpenForWriteAsync(string path, FileWriteMode mode, UnixFileMode createMode, CancellationToken cancellationToken)
        {
            Opened.Add("write " + path);
            return ValueTask.FromResult(FileOpenResult.Opened(new SavingStream(bytes => Files[path] = bytes), 0, null));
        }

        private sealed class SavingStream(Action<byte[]> save) : MemoryStream
        {
            protected override void Dispose(bool disposing)
            {
                save(ToArray());
                base.Dispose(disposing);
            }
        }
    }

    /// <summary>Reports each connection its inner connector makes opened, numbered from 0, as <see cref="TcpConnector" /> does.</summary>
    private sealed class OpenReportingConnector(IConnector inner) : IConnector
    {
        public int Opened { get; private set; }

        public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            ConnectResult connected = await inner.ConnectAsync(target, cancellationToken);
            long connectionNumber = Opened++;
            target.Events.ReportConnectionOpened(new ConnectionOpenedEvent
            {
                HostName = target.Host,
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, target.Port),
                LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 50000),
                ConnectionNumber = connectionNumber,
            });
            return ConnectResult.Connected(connected.Connection!, null, connectionNumber: connectionNumber);
        }
    }
}
