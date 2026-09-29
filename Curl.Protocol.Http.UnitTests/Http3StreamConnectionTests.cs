using System.Net;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

[TestClass]
public sealed class Http3StreamConnectionTests
{
    [TestMethod]
    public async Task WriteAsync_EmptyBodyWrite_SendsNoDataFrame()
    {
        FakeMultiplexedStream request = new(0, []);
        IHttpStreamConnection stream = new Http3Session(new FakeMultiplexedConnection(request)).CreateStream("https", null);
        await stream.WriteAsync("GET / HTTP/1.1\r\nHost: a\r\n\r\n"u8.ToArray(), CancellationToken.None);
        long headLength = request.Written.Length;

        await stream.WriteAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await stream.FlushAsync(CancellationToken.None);

        Assert.AreEqual(headLength, request.Written.Length);
        Assert.IsFalse(request.IsEndedByClient);
    }

    [TestMethod]
    public async Task EndRequestAsync_AfterTheLastKnownBodyByte_SendsNothingMore()
    {
        FakeMultiplexedStream request = new(0, []);
        IHttpStreamConnection stream = new Http3Session(new FakeMultiplexedConnection(request)).CreateStream("https", 2);
        await stream.WriteAsync("POST / HTTP/1.1\r\nHost: a\r\n\r\n"u8.ToArray(), CancellationToken.None);
        await stream.WriteAsync("ab"u8.ToArray(), CancellationToken.None);
        long sent = request.Written.Length;

        await stream.EndRequestAsync(CancellationToken.None);

        Assert.AreEqual(sent, request.Written.Length);
        Assert.AreEqual(1, request.EndCount);
    }

    [TestMethod]
    public async Task Connection_Stream_IsSecureWithTheSessionsRemoteEndpointAndLeavesDisposalToTheSession()
    {
        IPEndPoint remote = new(IPAddress.Loopback, 443);
        FakeMultiplexedStream request = new(0, []);
        IHttpStreamConnection stream = new Http3Session(new FakeMultiplexedConnection(request) { RemoteEndPoint = remote }).CreateStream("https", 0);
        await stream.WriteAsync("GET / HTTP/1.1\r\nHost: a\r\n\r\n"u8.ToArray(), CancellationToken.None);

        await stream.DisposeAsync();

        Assert.IsTrue(stream.IsSecure);
        Assert.AreEqual(remote, stream.RemoteEndPoint);
        Assert.IsTrue(stream.TrailerBytes.IsEmpty);
        Assert.IsFalse(request.IsDisposed);
    }
}
