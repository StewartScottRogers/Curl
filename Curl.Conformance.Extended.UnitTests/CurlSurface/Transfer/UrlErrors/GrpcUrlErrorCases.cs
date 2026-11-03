using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>curl's URL errors (manual, "EXIT CODES" 3, 6 and 7) for <c>grpc</c>, <c>grpcs</c>.</summary>
[TestClass]
public sealed class GrpcUrlErrorCases
{
    internal static UrlErrorScheme For(string scheme) => scheme switch
    {
        "grpc" => new(scheme, Path: "/pkg.Svc/Say"),
        _ => new(scheme, Path: "/pkg.Svc/Say"),
    };

    [TestMethod]
    [SpecRequirement("CurlSurface:url-bad-port-grpc-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-grpc-1")]
    [SpecRequirement("CurlSurface:url-space-grpc-1")]
    [SpecRequirement("CurlSurface:url-bad-port-grpcs-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-grpcs-1")]
    [SpecRequirement("CurlSurface:url-space-grpcs-1")]
    [DataRow("grpc", UrlErrorChecks.BadPort)]
    [DataRow("grpc", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("grpc", UrlErrorChecks.Space)]
    [DataRow("grpcs", UrlErrorChecks.BadPort)]
    [DataRow("grpcs", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("grpcs", UrlErrorChecks.Space)]
    public Task MalformedUrl_EndsWithExit3AndCurlsReason(string scheme, string check) => UrlErrorChecks.MalformedAsync(For(scheme), check);

    [TestMethod]
    [SpecRequirement("CurlSurface:unresolvable-host-grpc-1")]
    [SpecRequirement("CurlSurface:unresolvable-host-grpcs-1")]
    [DataRow("grpc")]
    [DataRow("grpcs")]
    public Task UnresolvableHost_EndsWithExit6(string scheme) => UrlErrorChecks.UnresolvableHostAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:connection-refused-grpc-1")]
    [SpecRequirement("CurlSurface:connection-refused-grpcs-1")]
    [DataRow("grpc")]
    [DataRow("grpcs")]
    public Task ConnectionRefused_EndsWithExit7(string scheme) => UrlErrorChecks.ConnectionRefusedAsync(For(scheme));
}
