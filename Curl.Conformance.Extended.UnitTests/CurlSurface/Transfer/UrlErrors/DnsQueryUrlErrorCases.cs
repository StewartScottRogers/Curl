using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>curl's URL errors (manual, "EXIT CODES" 3, 6 and 7) for <c>dns</c>.</summary>
[TestClass]
public sealed class DnsQueryUrlErrorCases
{
    internal static UrlErrorScheme For(string scheme) => scheme switch
    {
        _ => new(scheme, Datagram: true),
    };

    [TestMethod]
    [SpecRequirement("CurlSurface:url-bad-port-dns-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-dns-1")]
    [SpecRequirement("CurlSurface:url-space-dns-1")]
    [DataRow("dns", UrlErrorChecks.BadPort)]
    [DataRow("dns", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("dns", UrlErrorChecks.Space)]
    public Task MalformedUrl_EndsWithExit3AndCurlsReason(string scheme, string check) => UrlErrorChecks.MalformedAsync(For(scheme), check);

    [TestMethod]
    [SpecRequirement("CurlSurface:unresolvable-host-dns-1")]
    [DataRow("dns")]
    public Task UnresolvableHost_EndsWithExit6(string scheme) => UrlErrorChecks.UnresolvableHostAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:connection-refused-dns-1")]
    [DataRow("dns")]
    public Task ConnectionRefused_EndsWithExit7(string scheme) => UrlErrorChecks.ConnectionRefusedAsync(For(scheme));
}
