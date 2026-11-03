using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>curl's URL errors (manual, "EXIT CODES" 3, 6 and 7) for <c>oci</c>.</summary>
[TestClass]
public sealed class OciRegistryUrlErrorCases
{
    internal static UrlErrorScheme For(string scheme) => scheme switch
    {
        _ => new(scheme),
    };

    [TestMethod]
    [SpecRequirement("CurlSurface:url-bad-port-oci-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-oci-1")]
    [SpecRequirement("CurlSurface:url-space-oci-1")]
    [DataRow("oci", UrlErrorChecks.BadPort)]
    [DataRow("oci", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("oci", UrlErrorChecks.Space)]
    public Task MalformedUrl_EndsWithExit3AndCurlsReason(string scheme, string check) => UrlErrorChecks.MalformedAsync(For(scheme), check);

    [TestMethod]
    [SpecRequirement("CurlSurface:unresolvable-host-oci-1")]
    [DataRow("oci")]
    public Task UnresolvableHost_EndsWithExit6(string scheme) => UrlErrorChecks.UnresolvableHostAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:connection-refused-oci-1")]
    [DataRow("oci")]
    public Task ConnectionRefused_EndsWithExit7(string scheme) => UrlErrorChecks.ConnectionRefusedAsync(For(scheme));
}
