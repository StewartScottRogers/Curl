using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>curl's URL errors (manual, "EXIT CODES" 3, 6 and 7) for <c>redis</c>, <c>rediss</c>.</summary>
[TestClass]
public sealed class RedisUrlErrorCases
{
    internal static UrlErrorScheme For(string scheme) => scheme switch
    {
        "redis" => new(scheme),
        _ => new(scheme),
    };

    [TestMethod]
    [SpecRequirement("CurlSurface:url-bad-port-redis-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-redis-1")]
    [SpecRequirement("CurlSurface:url-space-redis-1")]
    [SpecRequirement("CurlSurface:url-bad-port-rediss-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-rediss-1")]
    [SpecRequirement("CurlSurface:url-space-rediss-1")]
    [DataRow("redis", UrlErrorChecks.BadPort)]
    [DataRow("redis", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("redis", UrlErrorChecks.Space)]
    [DataRow("rediss", UrlErrorChecks.BadPort)]
    [DataRow("rediss", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("rediss", UrlErrorChecks.Space)]
    public Task MalformedUrl_EndsWithExit3AndCurlsReason(string scheme, string check) => UrlErrorChecks.MalformedAsync(For(scheme), check);

    [TestMethod]
    [SpecRequirement("CurlSurface:unresolvable-host-redis-1")]
    [SpecRequirement("CurlSurface:unresolvable-host-rediss-1")]
    [DataRow("redis")]
    [DataRow("rediss")]
    public Task UnresolvableHost_EndsWithExit6(string scheme) => UrlErrorChecks.UnresolvableHostAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:connection-refused-redis-1")]
    [SpecRequirement("CurlSurface:connection-refused-rediss-1")]
    [DataRow("redis")]
    [DataRow("rediss")]
    public Task ConnectionRefused_EndsWithExit7(string scheme) => UrlErrorChecks.ConnectionRefusedAsync(For(scheme));
}
