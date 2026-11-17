using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.OciRegistry.Client;

/// <summary>Every connection of an <c>oci://</c> pull carries the transfer's proxy (ADR-0056), so <c>-x</c> and <c>HTTPS_PROXY</c> are never bypassed.</summary>
[TestClass]
public sealed class OciRegistryProxyTests
{
    private const string Challenge = "401 Unauthorized\r\nWWW-Authenticate: Bearer realm=\"https://auth.example/token\",service=\"reg\",scope=\"repository:alpine:pull\"";
    private static readonly ProxyEndpoint Proxy = new(ProxyKind.Http, "proxy.test", 3128, null);

    [TestMethod]
    public async Task ExecuteAsync_ProxyGiven_RegistryAndTokenHopsCarryTheProxy()
    {
        var connector = new SequenceConnector(
            SequenceConnector.Respond(Challenge),
            SequenceConnector.Respond("200 OK", """{"token":"t"}"""),
            SequenceConnector.Respond("200 OK", "{}"));

        var result = await PullAsync(connector, Proxy);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(3, connector.Targets);
        Assert.IsTrue(connector.Targets.All(target => ReferenceEquals(Proxy, target.Proxy)));
        Assert.AreEqual("reg.example", connector.Targets[0].Host);
        Assert.AreEqual("auth.example", connector.Targets[1].Host);
    }

    [TestMethod]
    public async Task ExecuteAsync_RedirectToBlobStorage_BlobHopCarriesTheProxy()
    {
        var connector = new SequenceConnector(
            SequenceConnector.Respond("307 Temporary Redirect\r\nLocation: https://blobs.example/b/1"),
            SequenceConnector.Respond("200 OK", "blob"));

        var result = await PullAsync(connector, Proxy, "oci://reg.example/alpine@sha256:" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("blob"))));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("blobs.example", connector.Targets[1].Host);
        Assert.IsTrue(connector.Targets.All(target => ReferenceEquals(Proxy, target.Proxy)));
    }

    [TestMethod]
    public async Task ExecuteAsync_NoProxy_TargetIsUnchanged()
    {
        var connector = new SequenceConnector(SequenceConnector.Respond("200 OK", "{}"));

        await PullAsync(connector, null);

        Assert.AreEqual(new ConnectTarget("reg.example", 443, true), connector.Targets[0]);
        Assert.IsNull(connector.Targets[0].Proxy);
    }

    private static async Task<TransferResult> PullAsync(SequenceConnector connector, ProxyEndpoint? proxy, string url = "oci://reg.example/alpine:3")
    {
        using var output = new MemoryStream();
        return await new OciRegistryProtocolHandler(connector).ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse(url), Output = output, Proxy = proxy });
    }
}
