using System.Net;
using System.Net.Sockets;
using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Console;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Extended.UnitTests.Harness;

[TestClass]
public sealed class ExtendedConformanceHarnessTests
{
    private const int RedisPort = 6379;

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    // The client opens with RESP3's HELLO 3, then sends the URL's command.
    private const string RedisClientBytes = "*2\r\n$5\r\nHELLO\r\n$1\r\n3\r\n*1\r\n$4\r\nPING\r\n";

    private static ScriptedReferenceServer RedisScript() =>
        new(ScriptStep.ReceiveUntil("HELLO\r\n$1\r\n3\r\n"), ScriptStep.Send("%0\r\n"), ScriptStep.ReceiveUntil("PING\r\n"), ScriptStep.Send("+PONG\r\n"));

    private static ReferenceRoute RedisPong(int port) => ReferenceRoute.Tcp(port, RedisScript());

    [TestMethod]
    public async Task RunAsync_RedisPingOverTcp_ReportsExitCodeOutputAndWireBytes()
    {
        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(Curl, [RedisPong(RedisPort)], ["-s", "-m", "10", "redis://localhost/PING"]);

        Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        Assert.AreEqual("PONG\n", Encoding.Latin1.GetString(result.StandardOutput));
        Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardError));
        WireExchange exchange = result.Exchanges.Single();
        Assert.AreEqual(RedisPort, exchange.Port);
        Assert.AreEqual(ReferenceTransport.Tcp, exchange.Transport);
        Assert.AreEqual(RedisClientBytes, Encoding.Latin1.GetString(exchange.ClientBytes));
        Assert.AreEqual("%0\r\n+PONG\r\n", Encoding.Latin1.GetString(exchange.ServerBytes));
    }

    [TestMethod]
    public async Task RunAsync_TftpOverUdp_ReportsEachDatagram()
    {
        byte[] data = [0, 3, 0, 1, .. "hello"u8];
        ReferenceRoute route = ReferenceRoute.Udp(6969, new ScriptedDatagramServer(data));

        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(Curl, [route], ["-s", "tftp://localhost:6969/file"]);

        Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        Assert.AreEqual("hello", Encoding.Latin1.GetString(result.StandardOutput));
        WireExchange exchange = result.Exchanges.Single();
        Assert.AreEqual(ReferenceTransport.Udp, exchange.Transport);
        Assert.AreEqual(2, exchange.ClientSent.Count);
        StringAssert.StartsWith(Encoding.Latin1.GetString(exchange.ClientSent[0]), "\0\u0001file\0octet\0");
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 1 }, exchange.ClientSent[1]);
        CollectionAssert.AreEqual(data, exchange.ServerBytes);
        Assert.AreEqual(1, exchange.ServerSent.Count);
    }

    [TestMethod]
    public async Task RunAsync_CurlExtensionsZero_ReachesTheRunAndRefusesTheExtensionScheme()
    {
        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(
            Curl, [RedisPong(RedisPort)], ["redis://localhost/PING"], new Dictionary<string, string> { ["CURL_EXTENSIONS"] = "0" });

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(0, result.Exchanges.Count);
    }

    [TestMethod]
    public async Task RunAsync_PortNoRouteAnswers_FailsToConnect()
    {
        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(Curl, [RedisPong(RedisPort)], ["-s", "redis://localhost:6380/PING"]);

        Assert.AreEqual(7, result.ExitCode);
        Assert.AreEqual(0, result.Exchanges.Count);
    }

    [TestMethod]
    public async Task RunAsync_CurlClosesBeforeTheTerminator_EndsTheScript()
    {
        ReferenceRoute route = ReferenceRoute.Tcp(RedisPort, new ScriptedReferenceServer(ScriptStep.ReceiveUntil("never"), ScriptStep.Send("unsent")));

        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(Curl, [route], ["-s", "-m", "1", "redis://localhost/PING"]);

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.AreEqual(0, result.Exchanges.Single().ServerBytes.Length);
    }

    [TestMethod]
    public async Task DialFromAsync_DialsAsDialAsyncDoes()
    {
        using ReferenceRun run = new();
        ScriptedReferenceServer pong = new(ScriptStep.ReceiveUntil("PING\r\n"), ScriptStep.Send("+PONG\r\n"));
        ReferenceTcpDialer dialer = new([ReferenceRoute.Tcp(RedisPort, pong)], run);
        IPEndPoint server = new(IPAddress.Loopback, RedisPort);

        var dialed = await dialer.DialFromAsync(server, new IPEndPoint(IPAddress.Loopback, 0), 1, CancellationToken.None);
        await dialed.Connection.WriteAsync("*1\r\n$4\r\nPING\r\n"u8.ToArray(), CancellationToken.None);
        await dialed.Connection.FlushAsync(CancellationToken.None);
        byte[] buffer = new byte[7];
        int read = await dialed.Connection.ReadAsync(buffer, CancellationToken.None);
        await dialed.Connection.DisposeAsync();

        Assert.IsFalse(dialed.Connection.IsSecure);
        Assert.AreEqual(server, dialed.Connection.RemoteEndPoint);
        Assert.AreEqual("+PONG\r\n", Encoding.Latin1.GetString(buffer, 0, read));
        Assert.AreEqual(1, (await run.EndAsync()).Count);
    }

    [TestMethod]
    public async Task DialUnixSocketAsync_IsRefused()
    {
        using ReferenceRun run = new();
        ReferenceTcpDialer dialer = new([], run);

        await Assert.ThrowsExactlyAsync<SocketException>(async () => await dialer.DialUnixSocketAsync(new UnixSocketAddress("/tmp/x", false), CancellationToken.None));
    }

    [TestMethod]
    public async Task OpenAsync_PortNoUdpRouteAnswers_FailsToConnect()
    {
        using ReferenceRun run = new();
        ReferenceDatagramConnector connector = new([RedisPong(6969), ReferenceRoute.Udp(6970, new ScriptedDatagramServer([1]))], run);

        DatagramOpenResult opened = await connector.OpenAsync("localhost", 6969, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, opened.ExitCode);
    }

    [TestMethod]
    public async Task ReceiveAsync_ShortBuffer_CutsTheAnswer()
    {
        WireExchange exchange = new(1, ReferenceTransport.Udp);
        await using ReferenceDatagramChannel channel = new(new ScriptedDatagramServer([1, 2, 3]), exchange, new IPEndPoint(IPAddress.Loopback, 1));
        await channel.SendAsync(new byte[] { 9 }, channel.ServerEndPoint, CancellationToken.None);
        byte[] buffer = new byte[2];

        DatagramReceived received = await channel.ReceiveAsync(buffer, CancellationToken.None);

        Assert.AreEqual(2, received.Length);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, buffer);
    }

    [TestMethod]
    public void Answer_PastTheLastAnswer_StaysSilent()
    {
        ScriptedDatagramServer server = new([1]);

        Assert.AreEqual(1, server.Answer(Array.Empty<byte>()).Count);
        Assert.AreEqual(0, server.Answer(Array.Empty<byte>()).Count);
    }
}
