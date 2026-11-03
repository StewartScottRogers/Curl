using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>curl's URL errors (manual, "EXIT CODES" 3, 6 and 7) for <c>webtransport</c>.</summary>
[TestClass]
public sealed class WebTransportUrlErrorCases
{
    internal static UrlErrorScheme For(string scheme) => scheme switch
    {
        _ => new(scheme, Datagram: true),
    };

    [TestMethod]
    [SpecRequirement("CurlSurface:url-bad-port-webtransport-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-webtransport-1")]
    [SpecRequirement("CurlSurface:url-space-webtransport-1")]
    [DataRow("webtransport", UrlErrorChecks.BadPort)]
    [DataRow("webtransport", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("webtransport", UrlErrorChecks.Space)]
    public Task MalformedUrl_EndsWithExit3AndCurlsReason(string scheme, string check) => UrlErrorChecks.MalformedAsync(For(scheme), check);

    [TestMethod]
    [SpecRequirement("CurlSurface:unresolvable-host-webtransport-1")]
    [DataRow("webtransport")]
    public Task UnresolvableHost_EndsWithExit6(string scheme) => UrlErrorChecks.UnresolvableHostAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:connection-refused-webtransport-1")]
    [DataRow("webtransport")]
    public Task ConnectionRefused_EndsWithExit7(string scheme) => UrlErrorChecks.ConnectionRefusedAsync(For(scheme));
}
