using Curl.Networking.Fakes;

namespace Curl.Networking;

/// <summary>
/// CGR-286 (BL-3815): curl opens one DoH connection for each query running at once and reuses it, so
/// a name's A and AAAA queries take two <c>%{conn_id}</c> numbers and a second name's queries take none.
/// </summary>
[TestClass]
public sealed class Cgr286Tests
{
    [TestMethod]
    public async Task ResolveAsync_TwoNames_TakesOneConnectionIdForEachQueryRunningAtOnceOnly()
    {
        long taken = 0;
        var resolver = new DohDnsResolver(new FakeConnector(), new Uri("http://127.0.0.1:21800/dns-query")) { TakeConnectionId = () => taken++ };

        await resolver.ResolveAsync("foo.test", CancellationToken.None);
        await resolver.ResolveAsync("bar.test", CancellationToken.None);

        Assert.AreEqual(2, taken);
    }

    [TestMethod]
    public async Task ResolveAsync_NoTakerGiven_ResolvesWithoutTakingAnId()
    {
        var resolver = new DohDnsResolver(new FakeConnector(), new Uri("http://127.0.0.1:21800/dns-query"));

        await resolver.ResolveAsync("foo.test", CancellationToken.None);

        Assert.IsNull(resolver.TakeConnectionId);
    }
}
