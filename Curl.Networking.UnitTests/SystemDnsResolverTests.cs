using System.Net;
using System.Net.Sockets;

using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="SystemDnsResolver" />: an address literal resolves to itself without a
/// network, the system resolver's order is kept, and a lookup failure or a host longer than
/// <see cref="System.Net.Dns" /> accepts is an empty list.
/// </summary>
[TestClass]
public sealed class SystemDnsResolverTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ResolveAsync_WithAddressLiteral_ReturnsThatAddress()
    {
        var resolver = new SystemDnsResolver();

        Diagnostics.Arrange("host", "127.0.0.1");

        var addresses = await resolver.ResolveAsync("127.0.0.1", CancellationToken.None);

        Diagnostics.Act("addresses", string.Join(", ", addresses));
        Diagnostics.Assert("addresses", "127.0.0.1", string.Join(", ", addresses));

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

        Diagnostics.Arrange("host", "example.com");
        Diagnostics.Arrange("lookup answers", string.Join<IPAddress>(", ", looked));

        var addresses = await resolver.ResolveAsync("example.com", CancellationToken.None);

        Diagnostics.Act("addresses", string.Join(", ", addresses));
        Diagnostics.Act("requested host", requestedHost);
        Diagnostics.Assert("addresses", string.Join<IPAddress>(", ", looked), string.Join(", ", addresses));
        Diagnostics.Assert("requested host", "example.com", requestedHost);

        CollectionAssert.AreEqual(looked, addresses.ToArray());
        Assert.AreEqual("example.com", requestedHost);
    }

    [TestMethod]
    public async Task ResolveAsync_WhenLookupThrowsSocketException_ReturnsNoAddresses()
    {
        var resolver = new SystemDnsResolver(
            (_, _) => Task.FromException<IPAddress[]>(new SocketException((int)SocketError.HostNotFound)));

        Diagnostics.Arrange("host, lookup failure", "nonexistent.invalid, SocketException HostNotFound");

        var addresses = await resolver.ResolveAsync("nonexistent.invalid", CancellationToken.None);

        Diagnostics.Act("address count", addresses.Count);
        Diagnostics.Assert("address count", 0, addresses.Count);

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

        Diagnostics.Arrange("host length", hostLength);

        var addresses = await resolver.ResolveAsync(new string('a', hostLength), CancellationToken.None);

        Diagnostics.Act("address count", addresses.Count);
        Diagnostics.Assert("address count", 0, addresses.Count);

        Assert.IsEmpty(addresses);
    }

    [TestMethod]
    public async Task ResolveAsync_WhenLookupThrowsArgumentOutOfRangeException_ReturnsNoAddresses()
    {
        var resolver = new SystemDnsResolver(
            (_, _) => Task.FromException<IPAddress[]>(new ArgumentOutOfRangeException("hostNameOrAddress")));

        Diagnostics.Arrange("host, lookup failure", "host.example, ArgumentOutOfRangeException");

        var addresses = await resolver.ResolveAsync("host.example", CancellationToken.None);

        Diagnostics.Act("address count", addresses.Count);
        Diagnostics.Assert("address count", 0, addresses.Count);

        Assert.IsEmpty(addresses);
    }

    [TestMethod]
    public async Task ResolveAsync_WithWhitespaceHost_ThrowsArgumentException()
    {
        var resolver = new SystemDnsResolver();

        Diagnostics.Arrange("host", " ");

        var exception = await Assert.ThrowsExactlyAsync<ArgumentException>(
            async () => await resolver.ResolveAsync(" ", CancellationToken.None));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception", nameof(ArgumentException), exception.GetType().Name);
    }
}
