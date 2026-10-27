using System.Net;
using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-128: after a <c>CURLOPT_CONNECT_ONLY</c> perform, or a transfer an xferinfo or read callback aborted,
/// <c>CURLINFO_PRIMARY_IP</c>, <c>PRIMARY_PORT</c>, <c>LOCAL_IP</c>, <c>NUM_CONNECTS</c>, <c>SCHEME</c> and
/// <c>PROTOCOL</c> answer the connection made, and a read-aborted upload's <c>EFFECTIVE_METHOD</c> is <c>PUT</c>,
/// as libcurl 8.22.0's do.
/// </summary>
[TestClass]
public sealed class Cgr128Tests
{
    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 50000);

    private const string Response = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";

    [TestMethod]
    public async Task PerformAsync_ConnectOnly_AnswersTheConnectionsEndPointsSchemeAndProtocol()
    {
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(new ScriptedConnector([])).CreateEasy();
        easy.CreateConnectOnlyConnector = (_, _) => new EndPointConnector();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://127.0.0.1:21410/", ConnectOnly = ConnectOnly.Connect } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        AssertConnection(easy.Info, 21410);
    }

    [TestMethod]
    public async Task PerformAsync_ConnectOnlyWhoseConnectorGaveTheLocalEndPoint_AnswersIt()
    {
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(new ScriptedConnector([])).CreateEasy();
        easy.CreateConnectOnlyConnector = (_, _) => new EndPointConnector(localOnResult: true);
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "HTTP://127.0.0.1:8080/", ConnectOnly = ConnectOnly.Connect } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        AssertConnection(easy.Info, 8080);
        Assert.AreEqual(50000, easy.Info.LocalPort);
    }

    [TestMethod]
    public async Task PerformAsync_XferInfoCallbackAborts_AnswersTheConnectionsEndPointsSchemeAndProtocol()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]) { ReportsTheConnectionOpened = true };
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://127.0.0.1:21410/" },
            Behaviour = new EasyBehaviourOptions { NoProgress = false },
            Callbacks = new() { TransferInfo = (_, _, _, _) => 1 },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.AbortedByCallback, code);
        AssertConnection(easy.Info, 21410);
        Assert.AreEqual("GET", easy.Info.EffectiveMethod);
    }

    [TestMethod]
    public async Task PerformAsync_UploadWhoseReadCallbackAborts_AnswersPutAsTheEffectiveMethod()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n" + Response)]) { ReportsTheConnectionOpened = true };
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://127.0.0.1:21410/up" },
            Transfer = new EasyTransferOptions { Upload = true },
            Callbacks = new() { Read = _ => ReadCallbackResult.Abort },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.AbortedByCallback, code);
        Assert.AreEqual("PUT", easy.Info.EffectiveMethod);
        AssertConnection(easy.Info, 21410);
    }

    [TestMethod]
    [DataRow(null, false, false, false, false, "GET")]
    [DataRow("DELETE", true, true, true, false, "DELETE")]
    [DataRow(null, true, true, false, false, "HEAD")]
    [DataRow(null, false, true, true, false, "PUT")]
    [DataRow(null, false, false, true, false, "POST")]
    [DataRow(null, false, false, false, true, "POST")]
    public void EffectiveMethodOf_Options_AnswersLibcurlsMethod(string? custom, bool noBody, bool upload, bool post, bool postFields, string expected)
    {
        EasyOptions options = new()
        {
            Transfer = new EasyTransferOptions { CustomRequest = custom, NoBody = noBody, Upload = upload },
            Http = new() { Post = post, PostFields = postFields ? "a=b"u8.ToArray() : (ReadOnlyMemory<byte>?)null },
        };

        Assert.AreEqual(expected, ManagedEasyTransfer.EffectiveMethodOf(options));
    }

    [TestMethod]
    public void EffectiveMethodOf_PostFieldsUnderHttpGet_AnswersGet()
    {
        EasyOptions options = new() { Http = new() { HttpGet = true, PostFields = "a=b"u8.ToArray() } };

        Assert.AreEqual("GET", ManagedEasyTransfer.EffectiveMethodOf(options));
    }

    [TestMethod]
    public void WithOpenedConnection_OverAUnixSocket_AnswersTheUnixSocketsIpAsThePrimaryIp()
    {
        ConnectionOpenedEvent opened = new()
        {
            HostName = "h",
            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 80),
            LocalEndPoint = Local,
            UnixSocketRemoteIp = "/tmp/sock",
            ConnectionNumber = 0,
        };

        TransferInfo info = ManagedEasyTransfer.WithOpenedConnection(new TransferInfo(), opened);

        Assert.AreEqual("/tmp/sock", info.PrimaryIp);
        Assert.AreEqual(80, info.PrimaryPort);
        Assert.AreEqual(1, info.NumConnects);
    }

    [TestMethod]
    public void WithSchemeOf_UrlThatDoesNotParse_LeavesTheInfo()
    {
        TransferInfo info = new();

        Assert.AreSame(info, TransferInfoFromReport.WithSchemeOf(info, "not a url"));
    }

    private static void AssertConnection(TransferInfo info, long port)
    {
        Assert.AreEqual("127.0.0.1", info.PrimaryIp);
        Assert.AreEqual(port, info.PrimaryPort);
        Assert.AreEqual("127.0.0.1", info.LocalIp);
        Assert.AreEqual(1, info.NumConnects);
        Assert.AreEqual("http", info.Scheme);
        Assert.AreEqual(1, info.Protocol);
    }

    private static ManagedCurlEngine CreateEngine(IConnector connector) =>
        new(TimeProvider.System, (_, _, cookies) => CreateDispatcher(connector, cookies), readEnvironmentVariable: _ => null);

    private static ProtocolDispatcher CreateDispatcher(IConnector connector, ICookieStore? cookies) =>
        new(EngineComposition.CreateProtocolHandlers(
            connector,
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "Could not connect to server"),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            cookies));

    /// <summary>A connect-only connector whose connection carries the target's loopback end points.</summary>
    /// <param name="localOnResult">Whether the local end point is on the result, as the connector saw it, rather than on the connection.</param>
    private sealed class EndPointConnector(bool localOnResult = false) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(
                new EndPointConnection(new IPEndPoint(IPAddress.Loopback, target.Port), localOnResult ? null : Local),
                null,
                localOnResult ? Local : null));
    }

    /// <summary>A connection that reads nothing and reports its end points.</summary>
    /// <param name="remote">The peer's end point.</param>
    /// <param name="local">The local end point, or <see langword="null" />.</param>
    private sealed class EndPointConnection(IPEndPoint remote, IPEndPoint? local) : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => remote;

        public EndPoint? LocalEndPoint => local;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => ValueTask.FromResult(0);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
