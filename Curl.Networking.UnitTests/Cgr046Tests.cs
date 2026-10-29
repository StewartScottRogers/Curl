using System.Net;
using System.Net.Sockets;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// CGR-046 of the third-party gap report (BL-3575): curl 8.21.0's Schannel build resolves no IPv4
/// address in shorthand (<c>0</c>, <c>127.1</c>, <c>2130706433</c>, <c>127.0.0.01</c>,
/// <c>0x7f.0.0.1</c>), so <c>--connect-to</c> to one is exit 6 and <c>--interface</c> one exit 45,
/// while the OpenSSL build on Linux reads them as the system resolver does (ADR-0908, measured).
/// </summary>
[TestClass]
public sealed class Cgr046Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("0", true)]
    [DataRow("127.1", true)]
    [DataRow("127.0.1", true)]
    [DataRow("2130706433", true)]
    [DataRow("127.0.0.01", true)]
    [DataRow("0x7f.0.0.1", true)]
    [DataRow("127.0.0.1", false)]
    [DataRow("0.0.0.0", false)]
    [DataRow("::1", false)]
    [DataRow("foo.test", false)]
    public void IsShorthand_TellsAnIpv4AddressNotWrittenAsAStrictDottedQuad(string host, bool expected)
    {
        Diagnostics.Arrange("host", host);

        var shorthand = Ipv4ShorthandAddress.IsShorthand(host);

        Diagnostics.Act("shorthand", shorthand);
        Assert.AreEqual(expected, shorthand);
    }

    [TestMethod]
    [DataRow("127.1")]
    [DataRow("2130706433")]
    [DataRow("0")]
    public async Task ResolveAsync_RefusingIpv4Shorthand_ResolvesShorthandToNothingWithoutALookup(string host)
    {
        var lookedUp = new List<string>();
        var resolver = new SystemDnsResolver(
            (name, _) =>
            {
                lookedUp.Add(name);
                return Task.FromResult(new[] { IPAddress.Loopback });
            },
            refusesIpv4Shorthand: true);
        Diagnostics.Arrange("host", host);

        var addresses = await resolver.ResolveAsync(host, CancellationToken.None);

        Diagnostics.Act("addresses", addresses);
        Assert.IsEmpty(addresses);
        Assert.IsEmpty(lookedUp);
    }

    [TestMethod]
    public async Task ResolveAsync_RefusingIpv4Shorthand_StillLooksUpADottedQuad()
    {
        var resolver = new SystemDnsResolver((_, _) => Task.FromResult(new[] { IPAddress.Loopback }), refusesIpv4Shorthand: true);

        var addresses = await resolver.ResolveAsync("127.0.0.1", CancellationToken.None);

        Diagnostics.Act("addresses", addresses);
        CollectionAssert.AreEqual(new[] { IPAddress.Loopback }, addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_NotRefusingIpv4Shorthand_LooksShorthandUp()
    {
        var resolver = new SystemDnsResolver((_, _) => Task.FromResult(new[] { IPAddress.Loopback }), refusesIpv4Shorthand: false);

        var addresses = await resolver.ResolveAsync("127.1", CancellationToken.None);

        Diagnostics.Act("addresses", addresses);
        CollectionAssert.AreEqual(new[] { IPAddress.Loopback }, addresses.ToArray());
    }

    [TestMethod]
    [DataRow("127.1")]
    [DataRow("2130706433")]
    [DataRow("0")]
    public async Task ChooseAsync_WithAHostInIpv4ShorthandTheResolverRefuses_FailsTheInterface(string host)
    {
        var resolver = new FakeDnsResolver { HostsWithNoAddress = new HashSet<string> { host } };
        var chooser = new LocalBindingAddressChooser(new LocalBinding(null, host, null, 0, 1), new SystemNetworkInterfaceLookup(onWindows: true, () => []), resolver);
        Diagnostics.Arrange("host", host);

        var failure = await Assert.ThrowsExactlyAsync<LocalBindException>(
            async () => await chooser.ChooseAsync(AddressFamily.InterNetwork, NoTransferEvents.Instance, CancellationToken.None));

        Diagnostics.Act("failure", failure.Failure);
        Assert.AreEqual(LocalBindFailure.InterfaceFailed, failure.Failure);
        CollectionAssert.AreEqual(new[] { host }, resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task ChooseAsync_WithADottedQuadHost_BindsItWithoutTheResolver()
    {
        var resolver = new FakeDnsResolver();
        var chooser = new LocalBindingAddressChooser(new LocalBinding(null, "127.0.0.1", null, 0, 1), new SystemNetworkInterfaceLookup(onWindows: true, () => []), resolver);

        var address = await chooser.ChooseAsync(AddressFamily.InterNetwork, NoTransferEvents.Instance, CancellationToken.None);

        Diagnostics.Act("address", address);
        Assert.AreEqual(IPAddress.Loopback, address);
        Assert.IsEmpty(resolver.ResolvedHosts);
    }
}
