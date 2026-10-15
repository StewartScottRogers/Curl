using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="TelnetServerConnector"/> to upstream's <c>tests/negtelnetserver.py</c>: its four
/// negotiations on connect, the client's TELNET commands skipped, the first data echoed stripped, then a close.
/// </summary>
[TestClass]
public sealed class TelnetServerConnectorTests
{
    private static readonly byte[] ServerNegotiation = [255, 251, 39, 255, 252, 31, 255, 253, 39, 255, 254, 31];

    [TestMethod]
    public async Task Negotiation_IsReadableOnConnect()
    {
        IConnection connection = await ConnectAsync();
        byte[] buffer = new byte[64];

        int count = await connection.ReadAsync(buffer, CancellationToken.None);

        CollectionAssert.AreEqual(ServerNegotiation, buffer[..count]);
        CollectionAssert.AreEqual(ServerNegotiation, TelnetServerConnection.Negotiation);
    }

    [TestMethod]
    public async Task Data_IsEchoedStrippedOfWhitespaceAndCommands_ThenTheConnectionCloses()
    {
        IConnection connection = await ConnectAsync();
        await ReadAvailableAsync(connection);

        await connection.WriteAsync(new byte[] { 255, 251, 39, 255, 253, 3, (byte)' ', (byte)'t', 255, 241, (byte)'e', (byte)'s', (byte)'t', (byte)'\n' }, CancellationToken.None);
        await connection.WriteAsync("ignored"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual("test", await ReadToEndAsync(connection));
    }

    [TestMethod]
    public async Task Read_WhileOnlyCommandsArrive_WaitsForData()
    {
        IConnection connection = await ConnectAsync();
        await ReadAvailableAsync(connection);
        byte[] buffer = new byte[64];

        ValueTask<int> read = connection.ReadAsync(buffer, CancellationToken.None);
        await connection.WriteAsync(new byte[] { 255, 252, 31 }, CancellationToken.None);
        Assert.IsFalse(read.IsCompleted);
        await connection.WriteAsync("test1452\n"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual("test1452", Encoding.Latin1.GetString(buffer, 0, await read));
    }

    [TestMethod]
    public async Task Connection_IsPlainLoopbackAndClosesQuietly()
    {
        IConnection connection = await ConnectAsync();

        await connection.FlushAsync(CancellationToken.None);
        await connection.DisposeAsync();

        Assert.IsFalse(connection.IsSecure);
        Assert.AreEqual($"127.0.0.1:{TelnetServerConnector.TelnetPort}", connection.RemoteEndPoint!.ToString());
        CollectionAssert.AreEqual(new[] { "telnet" }, TelnetServerConnector.EmulatedServers.ToArray());
    }

    [TestMethod]
    public async Task OtherPort_ReachesTheWrappedServer()
    {
        TelnetServerConnector telnet = new(new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)));

        ConnectResult result = await telnet.ConnectAsync(new ConnectTarget("127.0.0.1", NoListenPortConnector.NoListenPort, false), CancellationToken.None);

        Assert.IsNull(result.Connection);
    }

    private static async Task<IConnection> ConnectAsync()
    {
        TelnetServerConnector telnet = new(new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)));
        return (await telnet.ConnectAsync(new ConnectTarget("127.0.0.1", TelnetServerConnector.TelnetPort, false), CancellationToken.None)).Connection!;
    }

    private static async Task ReadAvailableAsync(IConnection connection) =>
        await connection.ReadAsync(new byte[64], CancellationToken.None);

    private static async Task<string> ReadToEndAsync(IConnection connection)
    {
        StringBuilder reply = new();
        byte[] buffer = new byte[3];
        int count;
        while ((count = await connection.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            reply.Append(Encoding.Latin1.GetString(buffer, 0, count));
        }

        return reply.ToString();
    }
}
