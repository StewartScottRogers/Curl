using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>curl's URL errors (manual, "EXIT CODES" 3, 6 and 7) for <c>kafka</c>, <c>kafkas</c>.</summary>
[TestClass]
public sealed class KafkaUrlErrorCases
{
    internal static UrlErrorScheme For(string scheme) => scheme switch
    {
        "kafka" => new(scheme),
        _ => new(scheme),
    };

    [TestMethod]
    [SpecRequirement("CurlSurface:url-bad-port-kafka-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-kafka-1")]
    [SpecRequirement("CurlSurface:url-space-kafka-1")]
    [SpecRequirement("CurlSurface:url-bad-port-kafkas-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-kafkas-1")]
    [SpecRequirement("CurlSurface:url-space-kafkas-1")]
    [DataRow("kafka", UrlErrorChecks.BadPort)]
    [DataRow("kafka", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("kafka", UrlErrorChecks.Space)]
    [DataRow("kafkas", UrlErrorChecks.BadPort)]
    [DataRow("kafkas", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("kafkas", UrlErrorChecks.Space)]
    public Task MalformedUrl_EndsWithExit3AndCurlsReason(string scheme, string check) => UrlErrorChecks.MalformedAsync(For(scheme), check);

    [TestMethod]
    [SpecRequirement("CurlSurface:unresolvable-host-kafka-1")]
    [SpecRequirement("CurlSurface:unresolvable-host-kafkas-1")]
    [DataRow("kafka")]
    [DataRow("kafkas")]
    public Task UnresolvableHost_EndsWithExit6(string scheme) => UrlErrorChecks.UnresolvableHostAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:connection-refused-kafka-1")]
    [SpecRequirement("CurlSurface:connection-refused-kafkas-1")]
    [DataRow("kafka")]
    [DataRow("kafkas")]
    public Task ConnectionRefused_EndsWithExit7(string scheme) => UrlErrorChecks.ConnectionRefusedAsync(For(scheme));
}
