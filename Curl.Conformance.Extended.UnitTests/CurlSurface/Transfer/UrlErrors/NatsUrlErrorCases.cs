using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>curl's URL errors (manual, "EXIT CODES" 3, 6 and 7) for <c>nats</c>, <c>natss</c>.</summary>
[TestClass]
public sealed class NatsUrlErrorCases
{
    internal static UrlErrorScheme For(string scheme) => scheme switch
    {
        "nats" => new(scheme),
        _ => new(scheme),
    };

    [TestMethod]
    [SpecRequirement("CurlSurface:url-bad-port-nats-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-nats-1")]
    [SpecRequirement("CurlSurface:url-space-nats-1")]
    [SpecRequirement("CurlSurface:url-bad-port-natss-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-natss-1")]
    [SpecRequirement("CurlSurface:url-space-natss-1")]
    [DataRow("nats", UrlErrorChecks.BadPort)]
    [DataRow("nats", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("nats", UrlErrorChecks.Space)]
    [DataRow("natss", UrlErrorChecks.BadPort)]
    [DataRow("natss", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("natss", UrlErrorChecks.Space)]
    public Task MalformedUrl_EndsWithExit3AndCurlsReason(string scheme, string check) => UrlErrorChecks.MalformedAsync(For(scheme), check);

    [TestMethod]
    [SpecRequirement("CurlSurface:unresolvable-host-nats-1")]
    [SpecRequirement("CurlSurface:unresolvable-host-natss-1")]
    [DataRow("nats")]
    [DataRow("natss")]
    public Task UnresolvableHost_EndsWithExit6(string scheme) => UrlErrorChecks.UnresolvableHostAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:connection-refused-nats-1")]
    [SpecRequirement("CurlSurface:connection-refused-natss-1")]
    [DataRow("nats")]
    [DataRow("natss")]
    public Task ConnectionRefused_EndsWithExit7(string scheme) => UrlErrorChecks.ConnectionRefusedAsync(For(scheme));
}
