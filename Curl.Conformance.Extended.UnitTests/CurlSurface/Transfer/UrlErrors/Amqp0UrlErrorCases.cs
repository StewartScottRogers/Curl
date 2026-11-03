using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>curl's URL errors (manual, "EXIT CODES" 3, 6 and 7) for <c>amqp</c>, <c>amqps</c>.</summary>
[TestClass]
public sealed class Amqp0UrlErrorCases
{
    internal static UrlErrorScheme For(string scheme) => scheme switch
    {
        "amqp" => new(scheme, Path: "/v/q"),
        _ => new(scheme, Path: "/v/q"),
    };

    [TestMethod]
    [SpecRequirement("CurlSurface:url-bad-port-amqp-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-amqp-1")]
    [SpecRequirement("CurlSurface:url-space-amqp-1")]
    [SpecRequirement("CurlSurface:url-bad-port-amqps-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-amqps-1")]
    [SpecRequirement("CurlSurface:url-space-amqps-1")]
    [DataRow("amqp", UrlErrorChecks.BadPort)]
    [DataRow("amqp", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("amqp", UrlErrorChecks.Space)]
    [DataRow("amqps", UrlErrorChecks.BadPort)]
    [DataRow("amqps", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("amqps", UrlErrorChecks.Space)]
    public Task MalformedUrl_EndsWithExit3AndCurlsReason(string scheme, string check) => UrlErrorChecks.MalformedAsync(For(scheme), check);

    [TestMethod]
    [SpecRequirement("CurlSurface:unresolvable-host-amqp-1")]
    [SpecRequirement("CurlSurface:unresolvable-host-amqps-1")]
    [DataRow("amqp")]
    [DataRow("amqps")]
    public Task UnresolvableHost_EndsWithExit6(string scheme) => UrlErrorChecks.UnresolvableHostAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:connection-refused-amqp-1")]
    [SpecRequirement("CurlSurface:connection-refused-amqps-1")]
    [DataRow("amqp")]
    [DataRow("amqps")]
    public Task ConnectionRefused_EndsWithExit7(string scheme) => UrlErrorChecks.ConnectionRefusedAsync(For(scheme));
}
