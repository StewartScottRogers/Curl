using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>curl's URL errors (manual, "EXIT CODES" 3, 6 and 7) for <c>coap</c>, <c>coaps</c>.</summary>
[TestClass]
public sealed class CoapUrlErrorCases
{
    internal static UrlErrorScheme For(string scheme) => scheme switch
    {
        "coap" => new(scheme, Datagram: true),
        _ => new(scheme, Datagram: true),
    };

    [TestMethod]
    [SpecRequirement("CurlSurface:url-bad-port-coap-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-coap-1")]
    [SpecRequirement("CurlSurface:url-space-coap-1")]
    [SpecRequirement("CurlSurface:url-bad-port-coaps-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-coaps-1")]
    [SpecRequirement("CurlSurface:url-space-coaps-1")]
    [DataRow("coap", UrlErrorChecks.BadPort)]
    [DataRow("coap", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("coap", UrlErrorChecks.Space)]
    [DataRow("coaps", UrlErrorChecks.BadPort)]
    [DataRow("coaps", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("coaps", UrlErrorChecks.Space)]
    public Task MalformedUrl_EndsWithExit3AndCurlsReason(string scheme, string check) => UrlErrorChecks.MalformedAsync(For(scheme), check);

    [TestMethod]
    [SpecRequirement("CurlSurface:unresolvable-host-coap-1")]
    [SpecRequirement("CurlSurface:unresolvable-host-coaps-1")]
    [DataRow("coap")]
    [DataRow("coaps")]
    public Task UnresolvableHost_EndsWithExit6(string scheme) => UrlErrorChecks.UnresolvableHostAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:connection-refused-coap-1")]
    [SpecRequirement("CurlSurface:connection-refused-coaps-1")]
    [DataRow("coap")]
    [DataRow("coaps")]
    public Task ConnectionRefused_EndsWithExit7(string scheme) => UrlErrorChecks.ConnectionRefusedAsync(For(scheme));
}
