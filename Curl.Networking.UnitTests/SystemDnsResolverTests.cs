using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="SystemDnsResolver" />: an address literal resolves to itself without a
/// network, the system resolver's order is kept, and a lookup failure or a host longer than
/// <see cref="System.Net.Dns" /> accepts is an empty list.
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
    [DataRow(256)]
    [DataRow(300)]
    [DataRow(65535)]
    public async Task ResolveAsync_WithHostLongerThan255Characters_ReturnsNoAddressesWithoutThrowing(int hostLength)
    {
        // Dns.GetHostAddressesAsync throws ArgumentOutOfRangeException for a name over 255
        // characters, before any lookup. curl 8.21.0 (Schannel) accepts hosts up to 65535 bytes:
        // curl http://<300 a's>/ -> exit 6, curl: (6) Could not resolve host: <first 231 a's>
        // (measured 2026-09-27; the message is cut to curl's 255-byte error buffer, CurlErrorBuffer).
        var resolver = new SystemDnsResolver();

        var addresses = await resolver.ResolveAsync(new string('a', hostLength), CancellationToken.None);

        Assert.IsEmpty(addresses);
    }

    [TestMethod]
    public async Task ResolveAsync_WhenLookupThrowsArgumentOutOfRangeException_ReturnsNoAddresses()
    {
        var resolver = new SystemDnsResolver(
            (_, _) => Task.FromException<IPAddress[]>(new ArgumentOutOfRangeException("hostNameOrAddress")));

        var addresses = await resolver.ResolveAsync("host.example", CancellationToken.None);

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
