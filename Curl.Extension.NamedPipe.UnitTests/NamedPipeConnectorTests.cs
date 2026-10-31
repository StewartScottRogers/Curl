using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.NamedPipe;

/// <summary>
/// Checks <see cref="NamedPipeConnector" /> against Microsoft's "Named Pipes": a client opens the pipe by its name and,
/// in byte mode, reads and writes a byte stream with no framing of its own.
/// </summary>
[TestClass]
public sealed class NamedPipeConnectorTests
{
    private static readonly NamedPipeName Pipe = new(".", "docker_engine");

    [TestMethod]
    public async Task ConnectAsync_ByteMode_CarriesTheRequestAndResponseBytesUnchanged()
    {
        byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");
        FakeNamedPipeOpener opener = new() { ServerBytes = response };
        byte[] request = Encoding.ASCII.GetBytes("GET /_ping HTTP/1.1\r\nHost: localhost\r\n\r\n");

        ConnectResult result = await new NamedPipeConnector(Pipe, opener).ConnectAsync(new ConnectTarget("localhost", 80, UseTls: false), CancellationToken.None);
        await using IConnection connection = result.Connection!;
        await connection.WriteAsync(request, CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        byte[] read = new byte[response.Length + 8];
        int count = await connection.ReadAsync(read, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(request, opener.LastPipe!.Written.ToArray());
        Assert.AreEqual(1, opener.LastPipe.FlushCount);
        CollectionAssert.AreEqual(response, read[..count]);
        Assert.IsFalse(connection.IsSecure);
        Assert.IsNull(connection.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ConnectAsync_AnyHostAndPort_OpensTheOnePipe()
    {
        FakeNamedPipeOpener opener = new();
        NamedPipeConnector connector = new(Pipe, opener);

        await (await connector.ConnectAsync(new ConnectTarget("example.com", 8080, UseTls: false), CancellationToken.None)).Connection!.DisposeAsync();
        await (await connector.ConnectAsync(new ConnectTarget("other.test", 80, UseTls: false), CancellationToken.None)).Connection!.DisposeAsync();

        CollectionAssert.AreEqual(new[] { Pipe, Pipe }, opener.Opened);
    }

    [TestMethod]
    public async Task DisposeAsync_ClosesThePipe()
    {
        FakeNamedPipeOpener opener = new();

        ConnectResult result = await new NamedPipeConnector(Pipe, opener).ConnectAsync(new ConnectTarget("localhost", 80, UseTls: false), CancellationToken.None);
        await result.Connection!.DisposeAsync();

        Assert.IsTrue(opener.LastPipe!.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_TlsTarget_IsRefusedAsNotBuiltInWithoutOpeningThePipe()
    {
        FakeNamedPipeOpener opener = new();

        ConnectResult result = await new NamedPipeConnector(Pipe, opener).ConnectAsync(new ConnectTarget("localhost", 443, UseTls: true), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.NotBuiltIn, result.ExitCode);
        Assert.AreEqual(@"TLS over the named pipe \\.\pipe\docker_engine is not built in", result.ErrorMessage);
        Assert.IsEmpty(opener.Opened);
    }

    [TestMethod]
    public async Task ConnectAsync_TlsTargetWithCurlsTlsProvider_RunsTheHandshakeOverThePipe()
    {
        byte[] clientHello = [0x16, 0x03, 0x01, 0x00, 0x04, 0x01, 0x00, 0x00, 0x00];
        byte[] serverHello = [0x16, 0x03, 0x03, 0x00, 0x04, 0x02, 0x00, 0x00, 0x00];
        byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n\r\n");
        FakeNamedPipeOpener opener = new() { ServerBytes = [.. serverHello, .. response] };
        RecordedHandshakeTlsProvider tls = new(clientHello, serverHello.Length);
        DistinctTransferEvents events = new();

        ConnectResult result = await new NamedPipeConnector(Pipe, opener, tls).ConnectAsync(new ConnectTarget("example.com", 443, UseTls: true) { Events = events }, CancellationToken.None);
        await using IConnection connection = result.Connection!;
        await connection.WriteAsync(Encoding.ASCII.GetBytes("GET /"), CancellationToken.None);
        byte[] read = new byte[64];
        int count = await connection.ReadAsync(read, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsTrue(connection.IsSecure);
        Assert.AreEqual("example.com", tls.TargetHost);
        Assert.AreSame(events, tls.Events);
        CollectionAssert.AreEqual(serverHello, tls.ServerHello);
        CollectionAssert.AreEqual((byte[])[.. clientHello, .. Encoding.ASCII.GetBytes("GET /")], opener.LastPipe!.Written.ToArray());
        CollectionAssert.AreEqual(response, read[..count]);
        CollectionAssert.AreEqual(new[] { Pipe }, opener.Opened);
    }

    [TestMethod]
    public async Task ConnectAsync_HttpsTarget_OffersCurlsHttpOverTlsListThroughAlpn()
    {
        string[] h2ThenHttp11 = ["h2", "http/1.1"];
        RecordedHandshakeTlsProvider tls = new([0x16], 0);
        NamedPipeConnector connector = new(Pipe, new FakeNamedPipeOpener(), tls, h2ThenHttp11);

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("localhost", 443, UseTls: true) { PoolScheme = "https" }, CancellationToken.None);
        await result.Connection!.DisposeAsync();

        CollectionAssert.AreEqual(h2ThenHttp11, tls.ApplicationProtocols!.ToArray());
        Assert.AreSame(h2ThenHttp11, connector.HttpOverTlsApplicationProtocols);
    }

    [TestMethod]
    public async Task ConnectAsync_HttpsTargetNamingItsOwnList_OffersThatList()
    {
        string[] h2Only = ["h2"];
        RecordedHandshakeTlsProvider tls = new([0x16], 0);

        ConnectResult result = await new NamedPipeConnector(Pipe, new FakeNamedPipeOpener(), tls, ["h2", "http/1.1"]).ConnectAsync(new ConnectTarget("localhost", 443, UseTls: true) { PoolScheme = "HTTPS", ApplicationProtocols = h2Only }, CancellationToken.None);
        await result.Connection!.DisposeAsync();

        CollectionAssert.AreEqual(h2Only, tls.ApplicationProtocols!.ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_HttpsTargetWithoutCurlsList_OffersHttp11Alone()
    {
        RecordedHandshakeTlsProvider tls = new([0x16], 0);
        NamedPipeConnector connector = new(Pipe, new FakeNamedPipeOpener(), tls);

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("localhost", 443, UseTls: true) { PoolScheme = "https" }, CancellationToken.None);
        await result.Connection!.DisposeAsync();

        CollectionAssert.AreEqual(new[] { "http/1.1" }, tls.ApplicationProtocols!.ToArray());
    }

    [TestMethod]
    [DataRow("imaps", DisplayName = "another TLS scheme")]
    [DataRow(null, DisplayName = "no pool scheme")]
    public async Task ConnectAsync_TlsTargetThatIsNotHttps_OffersNothingThroughAlpn(string? poolScheme)
    {
        RecordedHandshakeTlsProvider tls = new([0x16], 0);

        ConnectResult result = await new NamedPipeConnector(Pipe, new FakeNamedPipeOpener(), tls, ["h2", "http/1.1"]).ConnectAsync(new ConnectTarget("localhost", 993, UseTls: true) { PoolScheme = poolScheme, ApplicationProtocols = ["h2"] }, CancellationToken.None);
        await result.Connection!.DisposeAsync();

        Assert.IsEmpty(tls.ApplicationProtocols!);
    }

    [TestMethod]
    public async Task ConnectAsync_TlsTargetNamingATlsServerName_VerifiesThatName()
    {
        RecordedHandshakeTlsProvider tls = new([0x16], 0);

        ConnectResult result = await new NamedPipeConnector(Pipe, new FakeNamedPipeOpener(), tls).ConnectAsync(new ConnectTarget("localhost", 443, UseTls: true) { TlsServerName = "docker.test" }, CancellationToken.None);
        await result.Connection!.DisposeAsync();

        Assert.AreEqual("docker.test", tls.TargetHost);
    }

    [TestMethod]
    public async Task ConnectAsync_TlsHandshakeThatFails_GivesTheProvidersFailure()
    {
        FakeNamedPipeOpener opener = new() { ServerBytes = [0x15, 0x03, 0x03, 0x00, 0x02, 0x02, 0x30] };
        RecordedHandshakeTlsProvider tls = new([0x16, 0x03, 0x01], 7, ConnectResult.Failed(CurlExitCode.PeerFailedVerification, "SSL certificate problem: unable to get local issuer certificate"));

        ConnectResult result = await new NamedPipeConnector(Pipe, opener, tls).ConnectAsync(new ConnectTarget("localhost", 443, UseTls: true), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.AreEqual("SSL certificate problem: unable to get local issuer certificate", result.ErrorMessage);
        Assert.IsTrue(opener.LastPipe!.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_TlsTargetWhosePipeCannotBeOpened_FailsAsCouldNotConnectBeforeAnyHandshake()
    {
        RecordedHandshakeTlsProvider tls = new([0x16], 0);

        ConnectResult result = await new NamedPipeConnector(Pipe, new FakeNamedPipeOpener(new IOException("busy")), tls).ConnectAsync(new ConnectTarget("localhost", 443, UseTls: true), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsNull(tls.TargetHost);
    }

    [TestMethod]
    [DataRow(typeof(IOException), DisplayName = "not there or busy")]
    [DataRow(typeof(TimeoutException), DisplayName = "timed out")]
    [DataRow(typeof(UnauthorizedAccessException), DisplayName = "access denied")]
    public async Task ConnectAsync_PipeThatCannotBeOpened_FailsAsCouldNotConnect(Type failureType)
    {
        Exception failure = (Exception)Activator.CreateInstance(failureType, "the reason")!;

        ConnectResult result = await new NamedPipeConnector(Pipe, new FakeNamedPipeOpener(failure)).ConnectAsync(new ConnectTarget("localhost", 80, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(@"Failed to connect to the named pipe \\.\pipe\docker_engine: the reason", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_Cancelled_LetsTheCancellationThrough() =>
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await new NamedPipeConnector(Pipe, new FakeNamedPipeOpener(new OperationCanceledException())).ConnectAsync(new ConnectTarget("localhost", 80, UseTls: false), CancellationToken.None));

    [TestMethod]
    public async Task ConnectAsync_NullTarget_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await new NamedPipeConnector(Pipe, new FakeNamedPipeOpener()).ConnectAsync(null!, CancellationToken.None));
}
