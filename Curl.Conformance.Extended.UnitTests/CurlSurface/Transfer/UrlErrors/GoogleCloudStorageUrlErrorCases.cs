using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>curl's URL errors (manual, "EXIT CODES" 3, 6 and 7) for <c>gs</c>.</summary>
[TestClass]
public sealed class GoogleCloudStorageUrlErrorCases
{
    internal static UrlErrorScheme For(string scheme) => scheme switch
    {
        _ => new(scheme, EndpointVariable: "STORAGE_EMULATOR_HOST"),
    };

    [TestMethod]
    [SpecRequirement("CurlSurface:url-bad-port-gs-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-gs-1")]
    [SpecRequirement("CurlSurface:url-space-gs-1")]
    [DataRow("gs", UrlErrorChecks.BadPort)]
    [DataRow("gs", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("gs", UrlErrorChecks.Space)]
    public Task MalformedUrl_EndsWithExit3AndCurlsReason(string scheme, string check) => UrlErrorChecks.MalformedAsync(For(scheme), check);

    [TestMethod]
    [SpecRequirement("CurlSurface:unresolvable-host-gs-1")]
    [DataRow("gs")]
    public Task UnresolvableHost_EndsWithExit6(string scheme) => UrlErrorChecks.UnresolvableHostAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:connection-refused-gs-1")]
    [DataRow("gs")]
    public Task ConnectionRefused_EndsWithExit7(string scheme) => UrlErrorChecks.ConnectionRefusedAsync(For(scheme));
}
