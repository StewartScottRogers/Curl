using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>curl's URL errors (manual, "EXIT CODES" 3, 6 and 7) for <c>mqtt5</c>, <c>mqtt5s</c>, <c>mqtt5+ws</c>.</summary>
[TestClass]
public sealed class Mqtt5UrlErrorCases
{
    internal static UrlErrorScheme For(string scheme) => scheme switch
    {
        "mqtt5" => new(scheme),
        "mqtt5s" => new(scheme),
        _ => new(scheme),
    };

    [TestMethod]
    [SpecRequirement("CurlSurface:url-bad-port-mqtt5-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-mqtt5-1")]
    [SpecRequirement("CurlSurface:url-space-mqtt5-1")]
    [SpecRequirement("CurlSurface:url-bad-port-mqtt5s-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-mqtt5s-1")]
    [SpecRequirement("CurlSurface:url-space-mqtt5s-1")]
    [SpecRequirement("CurlSurface:url-bad-port-mqtt5+ws-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-mqtt5+ws-1")]
    [SpecRequirement("CurlSurface:url-space-mqtt5+ws-1")]
    [DataRow("mqtt5", UrlErrorChecks.BadPort)]
    [DataRow("mqtt5", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("mqtt5", UrlErrorChecks.Space)]
    [DataRow("mqtt5s", UrlErrorChecks.BadPort)]
    [DataRow("mqtt5s", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("mqtt5s", UrlErrorChecks.Space)]
    [DataRow("mqtt5+ws", UrlErrorChecks.BadPort)]
    [DataRow("mqtt5+ws", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("mqtt5+ws", UrlErrorChecks.Space)]
    public Task MalformedUrl_EndsWithExit3AndCurlsReason(string scheme, string check) => UrlErrorChecks.MalformedAsync(For(scheme), check);

    [TestMethod]
    [SpecRequirement("CurlSurface:unresolvable-host-mqtt5-1")]
    [SpecRequirement("CurlSurface:unresolvable-host-mqtt5s-1")]
    [SpecRequirement("CurlSurface:unresolvable-host-mqtt5+ws-1")]
    [DataRow("mqtt5")]
    [DataRow("mqtt5s")]
    [DataRow("mqtt5+ws")]
    public Task UnresolvableHost_EndsWithExit6(string scheme) => UrlErrorChecks.UnresolvableHostAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:connection-refused-mqtt5-1")]
    [SpecRequirement("CurlSurface:connection-refused-mqtt5s-1")]
    [SpecRequirement("CurlSurface:connection-refused-mqtt5+ws-1")]
    [DataRow("mqtt5")]
    [DataRow("mqtt5s")]
    [DataRow("mqtt5+ws")]
    public Task ConnectionRefused_EndsWithExit7(string scheme) => UrlErrorChecks.ConnectionRefusedAsync(For(scheme));
}
