using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Pins CGR-162 of the 2026-10-25 gap report: an empty <c>IPFS_PATH</c> counts as not set,
/// so the gateway comes from <c>$HOME/.ipfs/gateway</c>, as curl 8.21.0 (Schannel) fetched
/// <c>http://127.0.0.1:21802</c> from that file with <c>IPFS_PATH=</c> on 2026-10-26, where
/// the port failed with exit 37.
/// </summary>
[TestClass]
public sealed class Cgr162Tests
{
    [TestMethod]
    public void TryRewrite_EmptyIpfsPath_ReadsTheHomeGatewayFile()
    {
        var rewriter = new IpfsGatewayRewriter(
            name => name switch { "IPFS_PATH" => string.Empty, "HOME" => "/home/u", _ => null },
            path => path == "/home/u/.ipfs/gateway" ? "http://127.0.0.1:21802" : null);

        bool rewritten = rewriter.TryRewrite(CurlUrl.Parse("ipfs://cid/f"), null, out string? gatewayUrl, out _);

        Assert.IsTrue(rewritten);
        Assert.AreEqual("http://127.0.0.1:21802/ipfs/cid/f", gatewayUrl);
    }
}
