using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="SystemDnsResolver" />: an address literal resolves to itself without a
/// network, the system resolver's order is kept, and a lookup failure is an empty list.
/// </summary>
[TestClass]
public sealed class SystemDnsResolverTests
{
    [TestMethod]
    public async Task ResolveAsync_WithAddressLiteral_ReturnsThatAddress()
    {
        var resolver = new SystemDnsResolver();

        var addresses = await resolver.ResolveAsync("127.0.0.1", CancellationToken.None);

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("127.0.0.1") }, addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_KeepsTheLookupsOrderAndPassesTheHost()
    {
        IPAddress[] looked = [IPAddress.Parse("192.0.2.2"), IPAddress.Parse("192.0.2.1")];
        string? requestedHost = null;
        var resolver = new SystemDnsResolver((host, _) =>
        {
            requestedHost = host;
            return Task.FromResult(looked);
        });

        var addresses = await resolver.ResolveAsync("example.com", CancellationToken.None);

        CollectionAssert.AreEqual(looked, addresses.ToArray());
        Assert.AreEqual("example.com", requestedHost);
    }

    [TestMethod]
    public async Task ResolveAsync_WhenLookupThrowsSocketException_ReturnsNoAddresses()
    {
        var resolver = new SystemDnsResolver(
            (_, _) => Task.FromException<IPAddress[]>(new SocketException((int)SocketError.HostNotFound)));

        var addresses = await resolver.ResolveAsync("nonexistent.invalid", CancellationToken.None);

        Assert.IsEmpty(addresses);
    }

    [TestMethod]
    public async Task ResolveAsync_WithWhitespaceHost_ThrowsArgumentException()
    {
        var resolver = new SystemDnsResolver();

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            async () => await resolver.ResolveAsync(" ", CancellationToken.None));
    }
}
