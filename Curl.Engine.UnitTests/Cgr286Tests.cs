using System.Net.Sockets;
using System.Text;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-286 (BL-3815): the DoH resolver the engine composes takes the run's <c>%{conn_id}</c> for its DoH
/// connection, so the transfer's own connection is numbered after it, as curl 8.22.0 numbers it.
/// </summary>
[TestClass]
public sealed class Cgr286Tests
{
    private static readonly byte[] Forbidden = Encoding.Latin1.GetBytes("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n");

    [TestMethod]
    public async Task CreateDohResolver_GivenAnIdTaker_TakesAnIdForTheDohConnection()
    {
        long taken = 0;
        IDnsResolver resolver = EngineComposition.CreateDohResolver(
            "http://127.0.0.1:21800/dns-query", new ScriptedConnector([Forbidden]), AddressFamily.InterNetwork, takeConnectionId: () => taken++);

        await resolver.ResolveAsync("foo.test", CancellationToken.None);

        Assert.AreEqual(1, taken);
    }
}
