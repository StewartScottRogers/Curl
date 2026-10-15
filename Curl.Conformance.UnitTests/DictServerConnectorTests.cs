using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="DictServerConnector"/> to upstream's <c>tests/dictserver.py</c>: the greeting on
/// connect, <c>552 No matches</c> to the request, then a close.
/// </summary>
[TestClass]
public sealed class DictServerConnectorTests
{
    [TestMethod]
    public async Task Request_IsAnsweredWithNoMatchesAfterTheGreetingAndCloses()
    {
        IConnection connection = await ConnectAsync(new DictServerConnector(new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System))));

        await connection.WriteAsync("CLIENT libcurl 8.22.0\r\nDEFINE ! basic\r\nQUIT\r\n"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual("220 dictserver <xnooptions> <msgid@msgid>\n552 No matches\n", await ReadToEndAsync(connection));
    }

    [TestMethod]
    public async Task Greeting_IsReadableBeforeTheRequest()
    {
        IConnection connection = await ConnectAsync(new DictServerConnector(new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System))));
        byte[] buffer = new byte[64];

        int count = await connection.ReadAsync(buffer, CancellationToken.None);

        Assert.AreEqual(DictServerConnector.Greeting, Encoding.Latin1.GetString(buffer, 0, count));
        Assert.AreEqual($"127.0.0.1:{DictServerConnector.DictPort}", connection.RemoteEndPoint!.ToString());
    }

    [TestMethod]
    public async Task OtherPort_ReachesTheWrappedServer()
    {
        DictServerConnector dict = new(new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)));

        ConnectResult result = await dict.ConnectAsync(new ConnectTarget("127.0.0.1", NoListenPortConnector.NoListenPort, false), CancellationToken.None);

        Assert.IsNull(result.Connection);
    }

    [TestMethod]
    public void EmulatedServers_NamesDict()
    {
        CollectionAssert.AreEqual(new[] { "dict" }, DictServerConnector.EmulatedServers.ToArray());
    }

    private static async Task<IConnection> ConnectAsync(DictServerConnector dict) =>
        (await dict.ConnectAsync(new ConnectTarget("127.0.0.1", DictServerConnector.DictPort, false), CancellationToken.None)).Connection!;

    private static async Task<string> ReadToEndAsync(IConnection connection)
    {
        StringBuilder reply = new();
        byte[] buffer = new byte[8];
        int count;
        while ((count = await connection.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            reply.Append(Encoding.Latin1.GetString(buffer, 0, count));
        }

        return reply.ToString();
    }
}
