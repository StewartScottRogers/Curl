using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.EncryptedDns.DnsOverQuic;

[TestClass]
public sealed class DnsOverQuicResolverHookTests
{
    [TestMethod]
    public async Task ResolverOver_CurlsResolver_GivesADnsOverQuicResolverThatAnswersALiteralWithoutTheServer()
    {
        var curlResolver = new FixedDnsResolver(IPAddress.Parse("192.0.2.9"));
        var hook = new DnsOverQuicResolverHook(new DnsOverQuicServer("doq.example"));

        IDnsResolver resolver = hook.ResolverOver(curlResolver);
        IReadOnlyList<IPAddress> addresses = await resolver.ResolveAsync("[2001:db8::1]", CancellationToken.None);

        Assert.IsInstanceOfType<DnsOverQuicResolver>(resolver);
        Assert.AreEqual(IPAddress.Parse("2001:db8::1"), addresses.Single());
        Assert.AreEqual(0, curlResolver.LookupCount);
    }

    [TestMethod]
    public void ResolverOver_NullResolver_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new DnsOverQuicResolverHook(new DnsOverQuicServer("doq.example")).ResolverOver(null!));
}
