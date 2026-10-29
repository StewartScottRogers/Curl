using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.EncryptedDns.DnsOverTls;

[TestClass]
public sealed class DnsOverTlsResolverHookTests
{
    [TestMethod]
    public async Task ResolverOver_CurlsResolver_GivesADnsOverTlsResolverThatAnswersALiteralWithoutTheServer()
    {
        var curlResolver = new FixedDnsResolver(IPAddress.Parse("192.0.2.9"));
        var hook = new DnsOverTlsResolverHook(new DnsOverTlsServer("dot.example"));

        IDnsResolver resolver = hook.ResolverOver(curlResolver);
        IReadOnlyList<IPAddress> addresses = await resolver.ResolveAsync("192.0.2.1", CancellationToken.None);

        Assert.IsInstanceOfType<DnsOverTlsResolver>(resolver);
        Assert.AreEqual(IPAddress.Parse("192.0.2.1"), addresses.Single());
        Assert.AreEqual(0, curlResolver.LookupCount);
    }

    [TestMethod]
    public void ResolverOver_NullResolver_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new DnsOverTlsResolverHook(new DnsOverTlsServer("dot.example")).ResolverOver(null!));
}
