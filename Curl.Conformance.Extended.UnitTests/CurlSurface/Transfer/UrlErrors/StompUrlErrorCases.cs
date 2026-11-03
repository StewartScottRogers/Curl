using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>curl's URL errors (manual, "EXIT CODES" 3, 6 and 7) for <c>stomp</c>, <c>stomps</c>.</summary>
[TestClass]
public sealed class StompUrlErrorCases
{
    internal static UrlErrorScheme For(string scheme) => scheme switch
    {
        "stomp" => new(scheme),
        _ => new(scheme),
    };

    [TestMethod]
    [SpecRequirement("CurlSurface:url-bad-port-stomp-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-stomp-1")]
    [SpecRequirement("CurlSurface:url-space-stomp-1")]
    [SpecRequirement("CurlSurface:url-bad-port-stomps-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-stomps-1")]
    [SpecRequirement("CurlSurface:url-space-stomps-1")]
    [DataRow("stomp", UrlErrorChecks.BadPort)]
    [DataRow("stomp", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("stomp", UrlErrorChecks.Space)]
    [DataRow("stomps", UrlErrorChecks.BadPort)]
    [DataRow("stomps", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("stomps", UrlErrorChecks.Space)]
    public Task MalformedUrl_EndsWithExit3AndCurlsReason(string scheme, string check) => UrlErrorChecks.MalformedAsync(For(scheme), check);

    [TestMethod]
    [SpecRequirement("CurlSurface:unresolvable-host-stomp-1")]
    [SpecRequirement("CurlSurface:unresolvable-host-stomps-1")]
    [DataRow("stomp")]
    [DataRow("stomps")]
    public Task UnresolvableHost_EndsWithExit6(string scheme) => UrlErrorChecks.UnresolvableHostAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:connection-refused-stomp-1")]
    [SpecRequirement("CurlSurface:connection-refused-stomps-1")]
    [DataRow("stomp")]
    [DataRow("stomps")]
    public Task ConnectionRefused_EndsWithExit7(string scheme) => UrlErrorChecks.ConnectionRefusedAsync(For(scheme));
}
