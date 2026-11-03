using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Redis;

namespace Curl.Conformance.Extended.UnitTests.Redis;

/// <summary>The reference Redis server's own edges, driven by fixed client bytes rather than a curl run.</summary>
[TestClass]
public sealed class RedisReferenceServerTests
{
    [TestMethod]
    public async Task ServeAsync_TwoCommands_AnswersEachInChunks()
    {
        List<string> seen = [];

        string sent = await ServeAsync("*1\r\n$4\r\nPING\r\n*2\r\n$3\r\nGET\r\n$0\r\n\r\n", command =>
        {
            seen.Add(string.Join(' ', command.Select(Encoding.Latin1.GetString)));
            return ["+O"u8.ToArray(), "K\r\n"u8.ToArray()];
        });

        Assert.AreEqual("+OK\r\n+OK\r\n", sent);
        CollectionAssert.AreEqual(new[] { "PING", "GET " }, seen);
    }

    [TestMethod]
    public async Task ServeAsync_NothingSent_SendsNothing() =>
        Assert.AreEqual(string.Empty, await ServeAsync(string.Empty, _ => throw new AssertFailedException("no command expected")));

    [TestMethod]
    [DataRow("\r\n", DisplayName = "empty line")]
    [DataRow("$1\r\n", DisplayName = "bulk string where an array is due")]
    [DataRow("*x\r\n", DisplayName = "count not a number")]
    [DataRow("*1\rx\r\n", DisplayName = "CR not followed by LF")]
    [DataRow("*1\r\n*1\r\n", DisplayName = "array where a bulk string is due")]
    [DataRow("*1\r\n$1\r\nab\r\n", DisplayName = "bulk string longer than its length")]
    public async Task ServeAsync_MalformedRequest_Throws(string request) =>
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => ServeAsync(request, _ => []));

    [TestMethod]
    public async Task ServeAsync_ClosedInsideARequest_Throws() =>
        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => ServeAsync("*1\r\n$4\r\nPI", _ => []));

    [TestMethod]
    public async Task ServeAsync_NullConnection_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new RedisReferenceServer(_ => []).ServeAsync(null!, CancellationToken.None));

    private static async Task<string> ServeAsync(string client, Func<IReadOnlyList<byte[]>, IReadOnlyList<byte[]>> respond)
    {
        byte[] request = Encoding.Latin1.GetBytes(client);
        using MemoryStream stream = new();
        stream.Write(request);
        stream.Position = 0;

        await new RedisReferenceServer(respond).ServeAsync(new ReferenceConnection(stream, new WireExchange(6379, ReferenceTransport.Tcp)), CancellationToken.None);

        return Encoding.Latin1.GetString(stream.ToArray()[request.Length..]);
    }
}
