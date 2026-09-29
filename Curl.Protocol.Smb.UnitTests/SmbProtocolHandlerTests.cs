using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smb.Fakes;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins the <c>smb://</c> session opening against curl, measured on 2026-09-29
/// (<see cref="SmbRecordedExchange" />): the bytes sent, and the exit code and message of
/// each outcome - 3 for a path with no share, 67 with no user or a refused session setup,
/// 7 for a refused negotiate.
/// </summary>
[TestClass]
public sealed class SmbProtocolHandlerTests
{
    private const string Url = "smb://" + SmbRecordedExchange.Host + "/share/x.txt";

    [TestMethod]
    public void SupportedSchemes_AreSmbAndSmbs()
    {
        var handler = new SmbProtocolHandler(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection())));

        CollectionAssert.AreEqual(new[] { "smb", "smbs" }, handler.SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullConnector_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SmbProtocolHandler(null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        var handler = new SmbProtocolHandler(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection())));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handler.ExecuteAsync(null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_AcceptedSession_SendsCurlsNegotiateThenSessionSetupAndSucceeds()
    {
        var connection = new ScriptedConnection(SmbRecordedExchange.NegotiateResponse, SmbRecordedExchange.SessionSetupAccepted);

        TransferResult result = await Handler(connection).ExecuteAsync(Context(Url, new NetworkCredential("User", "Password")));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            SmbRecordedExchange.NegotiateRequest.Concat(SmbRecordedExchange.SessionSetupRequest).ToArray(),
            connection.Sent);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_DomainInUserName_SendsItAsTheDomain()
    {
        var connection = new ScriptedConnection(SmbRecordedExchange.NegotiateResponse, SmbRecordedExchange.SessionSetupAccepted);

        await Handler(connection).ExecuteAsync(Context(Url, new NetworkCredential(@"DOM\Us", "pw")));

        CollectionAssert.AreEqual(
            SmbRecordedExchange.DomainSessionSetupRequest,
            connection.Sent.Skip(SmbRecordedExchange.NegotiateRequest.Length).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedSessionSetup_Exits67LoginDenied()
    {
        var connection = new ScriptedConnection(SmbRecordedExchange.NegotiateResponse, SmbRecordedExchange.SessionSetupRefused);

        TransferResult result = await Handler(connection).ExecuteAsync(Context(Url, new NetworkCredential("User", "Password")));

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual("Login denied", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedNegotiate_Exits7WithoutSendingASessionSetup()
    {
        var connection = new ScriptedConnection(SmbRecordedExchange.NegotiateRefused);

        TransferResult result = await Handler(connection).ExecuteAsync(Context(Url, new NetworkCredential("User", "Password")));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Could not connect to server", result.ErrorMessage);
        CollectionAssert.AreEqual(SmbRecordedExchange.NegotiateRequest, connection.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoCredentials_Exits67LoginDeniedWithNothingSent()
    {
        var connection = new ScriptedConnection();

        TransferResult result = await Handler(connection).ExecuteAsync(Context(Url, null));

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual("Login denied", result.ErrorMessage);
        Assert.IsEmpty(connection.Sent);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_PathWithoutShare_Exits3BeforeConnecting()
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));

        TransferResult result = await new SmbProtocolHandler(connector).ExecuteAsync(
            Context("smb://h/x.txt", new NetworkCredential("User", "Password")));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("missing share in URL path for SMB", result.ErrorMessage);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectRefused_ReturnsTheConnectorsFailure()
    {
        var connector = new RecordingConnector(ConnectResult.Refused("Failed to connect"));

        TransferResult result = await new SmbProtocolHandler(connector).ExecuteAsync(Context(Url, null));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect", result.ErrorMessage);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    [DataRow("smb://h/s/f", "h", 445, false)]
    [DataRow("smbs://h/s/f", "h", 445, true)]
    [DataRow("smb://h:1445/s/f", "h", 1445, false)]
    public async Task ExecuteAsync_Url_ConnectsToItsHostAndPortWithTlsForSmbs(string url, string host, int port, bool useTls)
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));

        await new SmbProtocolHandler(connector).ExecuteAsync(Context(url, null));

        Assert.AreEqual(new ConnectTarget(host, port, useTls), connector.Targets.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_ContextWithProxy_TunnelsThroughIt()
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));
        var proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null);
        var context = new TransferContext { Url = CurlUrl.Parse("smb://h/s/f"), Output = new MemoryStream(), Proxy = proxy };

        await new SmbProtocolHandler(connector).ExecuteAsync(context);

        Assert.AreEqual(new ConnectTarget("h", 445, false) { Proxy = proxy }, connector.Targets.Single());
    }

    private static SmbProtocolHandler Handler(ScriptedConnection connection) =>
        new(new RecordingConnector(ConnectResult.Connected(connection)), SmbCurlOperatingSystem.Linux);

    private static TransferContext Context(string url, NetworkCredential? credentials) =>
        new() { Url = CurlUrl.Parse(url), Output = new MemoryStream(), Credentials = credentials };
}
