using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

[TestClass]
public sealed class SocksServerConnectorTests
{
    private const string Get = "GET /1 HTTP/1.1\r\n\r\n";

    private static readonly byte[] Socks5NoAuthGreeting = [5, 1, 0];

    private static readonly byte[] Socks5Ipv4Connect = [5, 1, 0, 1, 127, 0, 0, 1, 0x23, 0x1E];

    [TestMethod]
    public async Task Socks4_ConnectsAndRelaysToTheRequestedPort()
    {
        (SocksServerConnector socks, SwsHttpServerConnector server) = Server(string.Empty);
        IConnection connection = await ConnectAsync(socks);

        await connection.WriteAsync(new byte[] { 4, 1, 0x23, 0x1E, 127, 0, 0, 1, (byte)'u' }, CancellationToken.None);
        await connection.WriteAsync(new byte[] { 0 }, CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 0, 90, 0x23, 0x1E, 127, 0, 0, 1 }, await ReadAsync(connection));
        Assert.AreEqual("hello\n", await ExchangeAsync(connection, Get));
        Assert.AreEqual(Get, Encoding.Latin1.GetString(server.ReceivedBytes.Span));
    }

    [TestMethod]
    public async Task Socks4a_ConnectsToTheNamedHost()
    {
        (SocksServerConnector socks, _) = Server(string.Empty);
        IConnection connection = await ConnectAsync(socks);

        await connection.WriteAsync(new byte[] { 4, 1, 0x23, 0x1E, 0, 0, 0, 1, 0, (byte)'h' }, CancellationToken.None);
        Assert.AreEqual(0, (await ReadAsync(connection)).Length);
        await connection.WriteAsync(new byte[] { 0 }, CancellationToken.None);

        Assert.AreEqual(90, (await ReadAsync(connection))[1]);
        Assert.AreEqual("hello\n", await ExchangeAsync(connection, Get));
    }

    [TestMethod]
    public async Task Socks5_NoAuthentication_ConnectsToAnIpv4Address()
    {
        (SocksServerConnector socks, _) = Server(string.Empty);
        IConnection connection = await ConnectAsync(socks);

        await connection.WriteAsync(new byte[] { 5 }, CancellationToken.None);
        await connection.WriteAsync(new byte[] { 1, 0 }, CancellationToken.None);
        CollectionAssert.AreEqual(new byte[] { 5, 0 }, await ReadAsync(connection));
        await connection.WriteAsync(Socks5Ipv4Connect.AsMemory(0, 4), CancellationToken.None);
        await connection.WriteAsync(Socks5Ipv4Connect.AsMemory(4), CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 5, 0, 0, 1, 127, 0, 0, 1, 0x23, 0x1E }, await ReadAsync(connection));
        Assert.AreEqual("hello\n", await ExchangeAsync(connection, Get));
    }

    [TestMethod]
    public async Task Socks5_ConnectsToAnIpv6AddressAndAHostName()
    {
        (SocksServerConnector socks, _) = Server(string.Empty);
        IConnection ipv6 = await ConnectAsync(socks);
        IConnection named = await ConnectAsync(socks);

        await ipv6.WriteAsync(Socks5NoAuthGreeting, CancellationToken.None);
        await ipv6.WriteAsync((byte[])[5, 1, 0, 4, .. new byte[15], 1, 0x23, 0x1E], CancellationToken.None);
        await named.WriteAsync((byte[])[.. Socks5NoAuthGreeting, 5, 1, 0, 3, 1, (byte)'h', 0x23, 0x1E], CancellationToken.None);

        Assert.AreEqual(22, (await ReadAsync(ipv6)).Length - 2);
        Assert.AreEqual(8, (await ReadAsync(named)).Length - 2);
        Assert.AreEqual("hello\n", await ExchangeAsync(named, Get));
    }

    [TestMethod]
    public async Task Socks5_RightCredentials_ConnectsToTheBackendPort()
    {
        (SocksServerConnector socks, _) = Server("method 2\nuser u\npassword p\nbackendport 8990\nflag\n");
        IConnection connection = await ConnectAsync(socks);

        await connection.WriteAsync(new byte[] { 5, 1, 2 }, CancellationToken.None);
        CollectionAssert.AreEqual(new byte[] { 5, 2 }, await ReadAsync(connection));
        await connection.WriteAsync(new byte[] { 1 }, CancellationToken.None);
        await connection.WriteAsync(new byte[] { 1, (byte)'u' }, CancellationToken.None);
        await connection.WriteAsync(new byte[] { 1, (byte)'p' }, CancellationToken.None);
        CollectionAssert.AreEqual(new byte[] { 1, 0 }, await ReadAsync(connection));
        await connection.WriteAsync((byte[])[5, 1, 0, 1, 127, 0, 0, 1, 0, 1], CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);

        Assert.AreEqual(10, (await ReadAsync(connection)).Length);
        Assert.AreEqual("hello\n", await ExchangeAsync(connection, Get));
        await connection.DisposeAsync();
    }

    [TestMethod]
    public async Task Socks5_WrongCredentials_AnswersOneAndCloses()
    {
        (SocksServerConnector socks, _) = Server("method 2\n");
        IConnection connection = await ConnectAsync(socks);

        await connection.WriteAsync((byte[])[5, 1, 2, 1, 1, (byte)'u', 1, (byte)'x'], CancellationToken.None);
        await connection.WriteAsync(Socks5Ipv4Connect, CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 5, 2, 1, 1 }, await ReadAsync(connection));
        Assert.AreEqual(0, (await ReadAsync(connection)).Length);
    }

    [TestMethod]
    [DataRow(new byte[] { 9 }, DisplayName = "an unknown version")]
    [DataRow(new byte[] { 1 }, DisplayName = "credentials before a greeting")]
    [DataRow(new byte[] { 5, 1, 2, 5, 1, 0, 1 }, DisplayName = "a request before the credentials")]
    [DataRow(new byte[] { 5, 1, 0, 5, 2, 0, 1, 127, 0, 0, 1, 0, 1 }, DisplayName = "a BIND request")]
    [DataRow(new byte[] { 5, 1, 0, 5, 1, 0, 9, 0, 0 }, DisplayName = "an unknown address type")]
    public async Task UnacceptableMessage_ClosesWithoutConnecting(byte[] request)
    {
        (SocksServerConnector socks, _) = Server("method 2\n".Substring(0, request.Length > 2 && request[2] == 2 ? 9 : 0));
        IConnection connection = await ConnectAsync(socks);

        await connection.WriteAsync(request, CancellationToken.None);
        await connection.WriteAsync(new byte[] { 5 }, CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        await connection.DisposeAsync();

        Assert.IsTrue((await ReadAsync(connection)).Length <= 2);
    }

    [TestMethod]
    [DataRow(new byte[] { 5 }, DisplayName = "a version alone")]
    [DataRow(new byte[] { 5, 2, 0 }, DisplayName = "a greeting short of its methods")]
    [DataRow(new byte[] { 5, 1, 2, 1 }, DisplayName = "credentials with no user length")]
    [DataRow(new byte[] { 5, 1, 2, 1, 1, (byte)'u' }, DisplayName = "credentials with no password length")]
    [DataRow(new byte[] { 5, 1, 2, 1, 1, (byte)'u', 2, (byte)'p' }, DisplayName = "credentials short of the password")]
    [DataRow(new byte[] { 5, 1, 0, 5, 1, 0 }, DisplayName = "a request with no address type")]
    [DataRow(new byte[] { 5, 1, 0, 5, 1, 0, 1, 127 }, DisplayName = "a request short of its address")]
    [DataRow(new byte[] { 4, 1, 0, 1, 127 }, DisplayName = "a SOCKS4 request short of its address")]
    public async Task IncompleteMessage_WaitsForTheRest(byte[] request)
    {
        (SocksServerConnector socks, _) = Server("method 2\nuser u\npassword p\n".Substring(0, request.Length > 2 && request[2] == 2 ? 27 : 0));
        IConnection connection = await ConnectAsync(socks);

        await connection.WriteAsync(request, CancellationToken.None);
        byte[] reply = await ReadAsync(connection);

        Assert.IsTrue(reply.Length is 0 or 2, $"{reply.Length} bytes");
        Assert.AreEqual(0, (await ReadAsync(connection)).Length);
    }

    [TestMethod]
    public async Task ConnectAsync_ToAnotherPort_ReachesTheWrappedServer()
    {
        (SocksServerConnector socks, _) = Server(string.Empty);

        IConnection connection = (await socks.ConnectAsync(new ConnectTarget("127.0.0.1", 8990, false), CancellationToken.None)).Connection!;

        Assert.IsNotInstanceOfType<SocksServerConnection>(connection);
        Assert.AreEqual("hello\n", await ExchangeAsync(connection, Get));
    }

    [TestMethod]
    public async Task Connect_BackendRefuses_Throws()
    {
        SocksServerConnector socks = new(ParsedTestCase.From(string.Empty), new RefusingConnector());
        IConnection connection = await ConnectAsync(socks);

        await Assert.ThrowsExactlyAsync<IOException>(async () => await connection.WriteAsync((byte[])[.. Socks5NoAuthGreeting, .. Socks5Ipv4Connect], CancellationToken.None));
    }

    private static (SocksServerConnector Socks, SwsHttpServerConnector Server) Server(string serverCommands)
    {
        SwsHttpServerConnector server = new(ParsedTestCase.From($"<reply>\n<data>\nHTTP/1.1 200 OK\nContent-Length: 6\n\nhello\n</data>\n<servercmd>\n{serverCommands}</servercmd>\n</reply>\n"));
        return (new SocksServerConnector(ParsedTestCase.From($"<reply>\n<servercmd>\n{serverCommands}</servercmd>\n</reply>\n"), server), server);
    }

    private static async Task<IConnection> ConnectAsync(SocksServerConnector socks)
    {
        ConnectResult result = await socks.ConnectAsync(new ConnectTarget("127.0.0.1", SocksServerConnector.SocksPort, false), CancellationToken.None);
        Assert.IsFalse(result.Connection!.IsSecure);
        Assert.AreEqual("127.0.0.1:8994", result.Connection.RemoteEndPoint!.ToString());
        return result.Connection;
    }

    private static async Task<byte[]> ReadAsync(IConnection connection)
    {
        byte[] buffer = new byte[64];
        int count = await connection.ReadAsync(buffer, CancellationToken.None);
        return buffer[..count];
    }

    private static async Task<string> ExchangeAsync(IConnection connection, string request)
    {
        await connection.WriteAsync(Encoding.Latin1.GetBytes(request), CancellationToken.None);
        StringBuilder reply = new();
        byte[] chunk;
        while ((chunk = await ReadAsync(connection)).Length > 0)
        {
            reply.Append(Encoding.Latin1.GetString(chunk));
        }

        string text = reply.ToString();
        return text[(text.IndexOf("\n\n", StringComparison.Ordinal) + 2)..];
    }

    private sealed class RefusingConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused"));
    }
}
