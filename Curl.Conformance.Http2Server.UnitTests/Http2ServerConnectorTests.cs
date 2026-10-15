using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Http2Server;

[TestClass]
public sealed class Http2ServerConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_ClientSendsThePreface_ServerAnswersWithItsSettingsOverThePlainInMemoryConnection()
    {
        var connector = new Http2ServerConnector(new RecordingBackendConnector(null));

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 8080, false), CancellationToken.None);
        IConnection connection = result.Connection!;
        await connection.WriteAsync("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8.ToArray(), CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        byte[] settings = new byte[Http2FrameCodec.HeaderLength + 6];
        int read = 0;
        while (read < settings.Length)
        {
            read += await connection.ReadAsync(settings.AsMemory(read), CancellationToken.None);
        }

        await connection.DisposeAsync();
        await connector.Sessions.Single();

        Assert.AreEqual((byte)Http2FrameType.Settings, settings[3]);
        Assert.IsFalse(connection.IsSecure);
        Assert.IsNull(connection.RemoteEndPoint);
    }
}
