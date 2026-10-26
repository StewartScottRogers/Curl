using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// CGR-083 of the third-party gap report (BL-3612): curl 8.22.0 opens a new connection to a
/// plain HTTP forward proxy for a <c>--next</c> transfer with different <c>-U</c> credentials,
/// so a forward proxy target's <see cref="ConnectTarget.ForwardProxy" /> credential is part of
/// its <see cref="ConnectionPoolKey" />.
/// </summary>
[TestClass]
public sealed class Cgr083Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Of_ForwardProxyTargetsWithDifferentCredentials_AreDifferentKeys()
    {
        var first = ForwardProxyTarget(new NetworkCredential("u", "p"));
        var second = ForwardProxyTarget(new NetworkCredential("v", "q"));
        Diagnostics.Arrange("first", first);
        Diagnostics.Arrange("second", second);

        var firstKey = ConnectionPoolKey.Of(first);
        var secondKey = ConnectionPoolKey.Of(second);

        Diagnostics.Act("firstKey", firstKey);
        Diagnostics.Act("secondKey", secondKey);
        Assert.AreNotEqual(firstKey, secondKey);
    }

    [TestMethod]
    public void Of_ForwardProxyTargetsWithEqualCredentials_AreOneKey()
    {
        var first = ForwardProxyTarget(new NetworkCredential("u", "p"));
        var second = ForwardProxyTarget(new NetworkCredential("u", "p"));

        var firstKey = ConnectionPoolKey.Of(first);
        var secondKey = ConnectionPoolKey.Of(second);

        Diagnostics.Act("firstKey", firstKey);
        Assert.AreEqual(firstKey, secondKey);
    }

    private static ConnectTarget ForwardProxyTarget(NetworkCredential credential) =>
        new("127.0.0.1", 20600, false)
        {
            IsForwardProxy = true,
            ForwardProxy = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 20600, credential),
            PoolScheme = "http",
        };
}
