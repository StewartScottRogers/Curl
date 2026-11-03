using System.Text;
using Curl.Conformance;
using Curl.Conformance.Extended.DnsQuery.Encrypted;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.DnsQuery.Encrypted;

/// <summary>
/// Drives <see cref="DohReferenceServer" /> directly over an in-memory pair, for the requests curl never sends
/// (BL-4681): none at all, one with no body, and one whose body is cut short.
/// </summary>
[TestClass]
public sealed class DohReferenceServerTests
{
    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ServeAsync_ClientClosesBeforeAWholeHead_RecordsNoRequest()
    {
        DohReferenceServer server = new((_, _) => []);
        (InMemoryDuplexStream serverEnd, InMemoryDuplexStream clientEnd) = InMemoryDuplexStream.CreatePair();
        Task serving = server.ServeAsync(Connection(serverEnd), CancellationToken.None);

        await clientEnd.WriteAsync("GET / HTTP/1.1\r\n"u8.ToArray());
        await clientEnd.DisposeAsync();
        await serving;

        Assert.IsEmpty(server.Received);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ServeAsync_GetWithNoContentLength_IsRecordedWithNoBodyAndAnsweredWithTheStatus()
    {
        DohReferenceServer server = new((_, _) => [], status: 400);
        (InMemoryDuplexStream serverEnd, InMemoryDuplexStream clientEnd) = InMemoryDuplexStream.CreatePair();
        Task serving = server.ServeAsync(Connection(serverEnd), CancellationToken.None);

        await clientEnd.WriteAsync("GET /dns-query?dns=AAABAAABAAAAAAAAB2V4YW1wbGUDY29tAAABAAE HTTP/1.1\r\nHost: localhost\r\n\r\n"u8.ToArray());
        await serving;
        byte[] response = new byte[200];
        int read = await clientEnd.ReadAsync(response);

        DohReceivedRequest request = server.Received.Single();
        Assert.AreEqual("GET /dns-query?dns=AAABAAABAAAAAAAAB2V4YW1wbGUDY29tAAABAAE HTTP/1.1", request.RequestLine);
        Assert.AreEqual("localhost", request.Headers["host"]);
        Assert.IsEmpty(request.Body);
        StringAssert.StartsWith(Encoding.ASCII.GetString(response, 0, read), "HTTP/1.1 400 ");
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ServeAsync_ClientClosesBeforeTheWholeBody_FailsTheCase()
    {
        DohReferenceServer server = new((_, _) => []);
        (InMemoryDuplexStream serverEnd, InMemoryDuplexStream clientEnd) = InMemoryDuplexStream.CreatePair();
        Task serving = server.ServeAsync(Connection(serverEnd), CancellationToken.None);

        await clientEnd.WriteAsync("POST /dns-query HTTP/1.1\r\nContent-Length: 29\r\n\r\n\0\0"u8.ToArray());
        await clientEnd.DisposeAsync();

        await Assert.ThrowsExactlyAsync<IOException>(async () => await serving);
        Assert.IsEmpty(server.Received);
    }

    private static ReferenceConnection Connection(Stream serverEnd) => new(serverEnd, new WireExchange(443, ReferenceTransport.Tls));
}
