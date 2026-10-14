using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="GopherServerConnector"/> to upstream's <c>sws --gopher</c>: one selector line,
/// the part its test number picks sent as written, then a close.
/// </summary>
[TestClass]
public sealed class GopherServerConnectorTests
{
    private const string Parts = "<reply>\n<data>\niMenu\n</data>\n<data3>\nthird\n</data3>\n<data7 nonewline=\"yes\">\nseventh\n</data7>\n</reply>\n";

    [TestMethod]
    public async Task Selector_IsAnsweredWithDataAndRecorded()
    {
        (GopherServerConnector gopher, IConnection connection) = await ConnectAsync(Parts);

        await connection.WriteAsync("/selector/12"u8.ToArray(), CancellationToken.None);
        await connection.WriteAsync("00\r\n"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual("iMenu\n", await ReadToEndAsync(connection));
        Assert.AreEqual("/selector/1200\r\n", Encoding.Latin1.GetString(gopher.ReceivedBytes.Span));
    }

    [TestMethod]
    [DataRow("/10003\r\n", "third\n")]
    [DataRow("/abc12340007\r\n", "seventh")]
    [DataRow("/x10000\r\n", "iMenu\n")]
    [DataRow("10003\r\n", "iMenu\n")]
    [DataRow("/sel/\r\n", "iMenu\n")]
    [DataRow("/99999999999999999999\r\n", "iMenu\n")]
    public async Task Selector_PicksThePartItsTestNumberNames(string selector, string reply)
    {
        (_, IConnection connection) = await ConnectAsync(Parts);

        await connection.WriteAsync(Encoding.Latin1.GetBytes(selector), CancellationToken.None);

        Assert.AreEqual(reply, await ReadToEndAsync(connection));
    }

    [TestMethod]
    public async Task Selector_NamingAMissingPart_IsAnsweredWithNothing()
    {
        (_, IConnection connection) = await ConnectAsync(Parts);

        await connection.WriteAsync("/10005\r\n"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual(string.Empty, await ReadToEndAsync(connection));
    }

    [TestMethod]
    public async Task BytesAfterTheRequest_AreNotRecorded()
    {
        (GopherServerConnector gopher, IConnection connection) = await ConnectAsync(Parts);

        await connection.WriteAsync("/1\r\nmore"u8.ToArray(), CancellationToken.None);
        await connection.WriteAsync("later"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual("/1\r\n", Encoding.Latin1.GetString(gopher.ReceivedBytes.Span));
        Assert.AreEqual(1, gopher.ReceivedDump.Count);
    }

    [TestMethod]
    public async Task Read_BeforeTheSelectorEnds_WaitsForTheNextWrite()
    {
        (_, IConnection connection) = await ConnectAsync(Parts);
        byte[] buffer = new byte[64];

        ValueTask<int> read = connection.ReadAsync(buffer, CancellationToken.None);
        Assert.IsFalse(read.IsCompleted);
        await connection.WriteAsync("/1\r"u8.ToArray(), CancellationToken.None);
        await connection.WriteAsync("\n"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual("iMenu\n", Encoding.Latin1.GetString(buffer, 0, await read));
    }

    [TestMethod]
    public async Task Connection_IsPlainLoopbackAndClosesQuietly()
    {
        (_, IConnection connection) = await ConnectAsync(Parts);

        await connection.FlushAsync(CancellationToken.None);
        await connection.DisposeAsync();

        Assert.IsFalse(connection.IsSecure);
        Assert.AreEqual($"127.0.0.1:{GopherServerConnector.GopherPort}", connection.RemoteEndPoint!.ToString());
    }

    [TestMethod]
    public async Task OtherPort_ReachesTheWrappedServer()
    {
        UpstreamTestCase testCase = ParsedTestCase.From(Parts);
        GopherServerConnector gopher = new(testCase, new NoListenPortConnector(new SwsHttpServerConnector(testCase, TimeProvider.System)));

        ConnectResult result = await gopher.ConnectAsync(new ConnectTarget("127.0.0.1", NoListenPortConnector.NoListenPort, false), CancellationToken.None);

        Assert.IsNull(result.Connection);
    }

    private static async Task<(GopherServerConnector Gopher, IConnection Connection)> ConnectAsync(string sections)
    {
        UpstreamTestCase testCase = ParsedTestCase.From(sections);
        GopherServerConnector gopher = new(testCase, new SwsHttpServerConnector(testCase, TimeProvider.System));
        ConnectResult result = await gopher.ConnectAsync(new ConnectTarget("127.0.0.1", GopherServerConnector.GopherPort, false), CancellationToken.None);
        return (gopher, result.Connection!);
    }

    private static async Task<string> ReadToEndAsync(IConnection connection)
    {
        StringBuilder reply = new();
        byte[] buffer = new byte[4];
        int count;
        while ((count = await connection.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            reply.Append(Encoding.Latin1.GetString(buffer, 0, count));
        }

        return reply.ToString();
    }
}
