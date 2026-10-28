using System.Net.Sockets;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-038 (ADR-0956): curl 8.22.0 sends a <c>--doh-url</c> with no scheme as <c>https://</c>,
/// measured on the Schannel build: <c>--doh-url 127.0.0.1:21840/dns-query</c> sent the listener a
/// TLS ClientHello (first bytes <c>16 03 01</c>) and the plain-HTTP server case ended in exit 28.
/// </summary>
[TestClass]
public sealed class Cgr038Tests
{
    [TestMethod]
    public void DohUrlOf_NoScheme_AssumesHttps()
    {
        Uri? url = EngineComposition.DohUrlOf("127.0.0.1:21801/dns-query");

        Assert.AreEqual(new Uri("https://127.0.0.1:21801/dns-query"), url);
    }

    [TestMethod]
    public async Task CreateDohResolver_NoScheme_ConnectsToTheDohServerOverTls()
    {
        ScriptedConnector connector = new([]);
        IDnsResolver resolver = EngineComposition.CreateDohResolver(
            "127.0.0.1:21801/dns-query", connector, AddressFamily.InterNetwork, readEnvironmentVariable: _ => null);

        await resolver.ResolveAsync("foo.test", CancellationToken.None);

        ConnectTarget target = connector.Targets.Single();
        Assert.AreEqual("127.0.0.1:21801", $"{target.Host}:{target.Port}");
        Assert.IsTrue(target.UseTls);
    }
}
