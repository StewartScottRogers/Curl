using System.Net;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

[TestClass]
public sealed class Http3SessionTests
{
    [TestMethod]
    public async Task OpenRequestStreamAsync_SecondRequest_OpensNoMoreUnidirectionalStreams()
    {
        FakeMultiplexedConnection quic = new(new FakeMultiplexedStream(0, []), new FakeMultiplexedStream(4, []));
        Http3Session session = new(quic);

        await session.OpenRequestStreamAsync(CancellationToken.None);
        var second = await session.OpenRequestStreamAsync(CancellationToken.None);

        Assert.AreEqual(4L, second.StreamId);
        Assert.HasCount(3, quic.UnidirectionalStreams);
        CollectionAssert.AreEqual(new long[] { 2, 6, 10 }, quic.UnidirectionalStreams.Select(stream => stream.StreamId).ToArray());
    }

    [TestMethod]
    public async Task DisposeAsync_AfterARequest_DisposesEveryStreamAndClosesWithNoError()
    {
        FakeMultiplexedStream request = new(0, []);
        FakeMultiplexedConnection quic = new(request);
        Http3Session session = new(quic);
        await session.OpenRequestStreamAsync(CancellationToken.None);

        await session.DisposeAsync();

        Assert.IsTrue(request.IsDisposed);
        Assert.IsTrue(quic.UnidirectionalStreams.TrueForAll(stream => stream.IsDisposed));
        Assert.AreEqual(0x100L, quic.CloseCode);
        Assert.IsTrue(quic.IsDisposed);
    }

    [TestMethod]
    public async Task Connection_Session_IsSecureWithTheQuicEndpointsAndCarriesNoBytesItself()
    {
        IPEndPoint remote = new(IPAddress.Loopback, 443);
        IPEndPoint local = new(IPAddress.Loopback, 50000);
        Http3Session session = new(new FakeMultiplexedConnection { RemoteEndPoint = remote, LocalEndPoint = local });

        Assert.IsTrue(session.IsSecure);
        Assert.AreEqual(remote, session.RemoteEndPoint);
        Assert.AreEqual(local, session.LocalEndPoint);
        Assert.AreEqual("HTTP/3", session.VersionName);
        Assert.AreEqual("using HTTP/3", session.UsingLine);
        Assert.IsTrue(session.AcceptsNewStreams);
        await session.FlushAsync(CancellationToken.None);
        await Assert.ThrowsExactlyAsync<NotSupportedException>(async () => await session.ReadAsync(new byte[1], CancellationToken.None));
        await Assert.ThrowsExactlyAsync<NotSupportedException>(async () => await session.WriteAsync(new byte[1], CancellationToken.None));
    }
}
