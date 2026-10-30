using System.Net;
using System.Text;
using Curl.Extension.ObliviousDoh.Http;

namespace Curl.Extension.ObliviousDoh;

[TestClass]
public sealed class ObliviousDohResolverTests
{
    private static readonly ObliviousDohEndpoints Endpoints = ObliviousDohEndpoints.Parse("https://proxy.example/proxy", "https://target.example/dns-query");

    [TestMethod]
    public async Task ResolveAsync_FetchesTheConfigsThenPostsAnAaaaAndAnAQueryThroughTheProxy()
    {
        using var target = new TestTarget();
        var connector = new FakeConnector(request => Serve(target, request));

        IReadOnlyList<IPAddress> addresses = await new ObliviousDohResolver(new ObliviousDohHttpClient(connector), Endpoints).ResolveAsync("www.example", CancellationToken.None);

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("2001:db8::1"), IPAddress.Parse("192.0.2.1") }, addresses.ToArray());
        CollectionAssert.AreEqual(new[] { "target.example", "proxy.example", "proxy.example" }, connector.Targets.Select(t => t.Host).ToArray());
        string post = Encoding.ASCII.GetString([.. connector.Connections[1].Written]);
        Assert.StartsWith("POST /proxy?targethost=target.example&targetpath=/dns-query HTTP/1.1\r\n", post);
    }

    [TestMethod]
    public async Task ResolveAsync_KeepsTheConfigsForTheNextName()
    {
        using var target = new TestTarget();
        var connector = new FakeConnector(request => Serve(target, request));
        var resolver = new ObliviousDohResolver(new ObliviousDohHttpClient(connector), Endpoints);

        await resolver.ResolveAsync("a.example", CancellationToken.None);
        await resolver.ResolveAsync("b.example", CancellationToken.None);

        Assert.AreEqual(1, connector.Targets.Count(t => t.Host == "target.example"));
    }

    [TestMethod]
    public async Task ResolveAsync_AnAddressLiteral_AsksNobody()
    {
        var connector = new FakeConnector(_ => []);

        IReadOnlyList<IPAddress> addresses = await new ObliviousDohResolver(new ObliviousDohHttpClient(connector), Endpoints).ResolveAsync("[::1]", CancellationToken.None);

        Assert.AreEqual(IPAddress.IPv6Loopback, addresses.Single());
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ResolveAsync_ConfigsNotServed_GivesNoAddresses() =>
        Assert.IsEmpty(await Resolve(_ => FakeConnector.Status(404), "www.example"));

    [TestMethod]
    public async Task ResolveAsync_NoUsableConfig_GivesNoAddresses() =>
        Assert.IsEmpty(await Resolve(_ => FakeConnector.Ok([0x00, 0x00]), "www.example"));

    [TestMethod]
    public async Task ResolveAsync_ConfigsUnreachable_GivesNoAddresses() =>
        Assert.IsEmpty(await new ObliviousDohResolver(new ObliviousDohHttpClient(new FakeConnector(_ => []) { Fails = true }), Endpoints).ResolveAsync("www.example", CancellationToken.None));

    [TestMethod]
    public async Task ResolveAsync_ProxyRefuses_GivesNoAddresses()
    {
        using var target = new TestTarget();

        Assert.IsEmpty(await Resolve(request => request[0] == (byte)'G' ? FakeConnector.Ok(target.ConfigsBytes()) : FakeConnector.Status(502), "www.example"));
    }

    [TestMethod]
    public async Task ResolveAsync_ResponseThatDoesNotOpen_GivesNoAddresses()
    {
        using var target = new TestTarget();

        Assert.IsEmpty(await Resolve(request => request[0] == (byte)'G' ? FakeConnector.Ok(target.ConfigsBytes()) : FakeConnector.Ok([0x02]), "www.example"));
    }

    [TestMethod]
    public async Task ResolveAsync_ANameNoQueryCanCarry_PostsNothing()
    {
        using var target = new TestTarget();
        var connector = new FakeConnector(request => Serve(target, request));

        Assert.IsEmpty(await new ObliviousDohResolver(new ObliviousDohHttpClient(connector), Endpoints).ResolveAsync(new string('a', 64) + ".example", CancellationToken.None));
        Assert.HasCount(1, connector.Targets);
    }

    [TestMethod]
    public async Task ResolveAsync_ATargetKeyHpkeCannotUse_PostsNothing()
    {
        byte[] badKeyConfigs = TestTarget.Vector([0x00, 0x01, .. TestTarget.Vector([0x00, 0x10, 0x00, 0x01, 0x00, 0x01, .. TestTarget.Vector([0x04, .. new byte[64]])])]);

        Assert.IsEmpty(await Resolve(_ => FakeConnector.Ok(badKeyConfigs), "www.example"));
    }

    [TestMethod]
    public async Task ResolveAsync_NoHost_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await new ObliviousDohResolver(new ObliviousDohHttpClient(new FakeConnector(_ => [])), Endpoints).ResolveAsync(" ", CancellationToken.None));

    private static async Task<IReadOnlyList<IPAddress>> Resolve(Func<byte[], byte[]> respond, string host) =>
        await new ObliviousDohResolver(new ObliviousDohHttpClient(new FakeConnector(respond)), Endpoints).ResolveAsync(host, CancellationToken.None);

    // The target serves its configs to a GET; the proxy relays a POST to the target, which answers it.
    private static byte[] Serve(TestTarget target, byte[] request) =>
        request[0] == (byte)'G'
            ? FakeConnector.Ok(target.ConfigsBytes())
            : FakeConnector.Ok(target.Respond(FakeConnector.BodyOf(request), TestTarget.AnswerTo));
}
