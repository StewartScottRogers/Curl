using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>curl's URL errors (manual, "EXIT CODES" 3, 6 and 7) for <c>s3</c>.</summary>
[TestClass]
public sealed class S3UrlErrorCases
{
    internal static UrlErrorScheme For(string scheme) => scheme switch
    {
        _ => new(scheme, EndpointVariable: "AWS_ENDPOINT_URL"),
    };

    [TestMethod]
    [SpecRequirement("CurlSurface:url-bad-port-s3-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-s3-1")]
    [SpecRequirement("CurlSurface:url-space-s3-1")]
    [DataRow("s3", UrlErrorChecks.BadPort)]
    [DataRow("s3", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("s3", UrlErrorChecks.Space)]
    public Task MalformedUrl_EndsWithExit3AndCurlsReason(string scheme, string check) => UrlErrorChecks.MalformedAsync(For(scheme), check);

    [TestMethod]
    [SpecRequirement("CurlSurface:unresolvable-host-s3-1")]
    [DataRow("s3")]
    public Task UnresolvableHost_EndsWithExit6(string scheme) => UrlErrorChecks.UnresolvableHostAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:connection-refused-s3-1")]
    [DataRow("s3")]
    public Task ConnectionRefused_EndsWithExit7(string scheme) => UrlErrorChecks.ConnectionRefusedAsync(For(scheme));
}
