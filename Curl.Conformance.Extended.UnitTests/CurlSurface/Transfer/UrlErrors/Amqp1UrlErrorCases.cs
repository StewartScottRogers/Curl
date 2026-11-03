using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>curl's URL errors (manual, "EXIT CODES" 3, 6 and 7) for <c>amqp1</c>, <c>amqp1s</c>.</summary>
[TestClass]
public sealed class Amqp1UrlErrorCases
{
    internal static UrlErrorScheme For(string scheme) => scheme switch
    {
        "amqp1" => new(scheme),
        _ => new(scheme),
    };

    [TestMethod]
    [SpecRequirement("CurlSurface:url-bad-port-amqp1-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-amqp1-1")]
    [SpecRequirement("CurlSurface:url-space-amqp1-1")]
    [SpecRequirement("CurlSurface:url-bad-port-amqp1s-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-amqp1s-1")]
    [SpecRequirement("CurlSurface:url-space-amqp1s-1")]
    [DataRow("amqp1", UrlErrorChecks.BadPort)]
    [DataRow("amqp1", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("amqp1", UrlErrorChecks.Space)]
    [DataRow("amqp1s", UrlErrorChecks.BadPort)]
    [DataRow("amqp1s", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("amqp1s", UrlErrorChecks.Space)]
    public Task MalformedUrl_EndsWithExit3AndCurlsReason(string scheme, string check) => UrlErrorChecks.MalformedAsync(For(scheme), check);

    [TestMethod]
    [SpecRequirement("CurlSurface:unresolvable-host-amqp1-1")]
    [SpecRequirement("CurlSurface:unresolvable-host-amqp1s-1")]
    [DataRow("amqp1")]
    [DataRow("amqp1s")]
    public Task UnresolvableHost_EndsWithExit6(string scheme) => UrlErrorChecks.UnresolvableHostAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:connection-refused-amqp1-1")]
    [SpecRequirement("CurlSurface:connection-refused-amqp1s-1")]
    [DataRow("amqp1")]
    [DataRow("amqp1s")]
    public Task ConnectionRefused_EndsWithExit7(string scheme) => UrlErrorChecks.ConnectionRefusedAsync(For(scheme));
}
