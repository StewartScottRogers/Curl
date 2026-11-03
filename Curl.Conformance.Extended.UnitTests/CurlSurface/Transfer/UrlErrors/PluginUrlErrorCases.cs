using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>curl's URL errors (manual, "EXIT CODES" 3, 6 and 7) for <c>demo</c>.</summary>
[TestClass]
public sealed class PluginUrlErrorCases
{
    internal static UrlErrorScheme For(string scheme) => scheme switch
    {
        _ => new(scheme, Helper: true),
    };

    [TestMethod]
    [SpecRequirement("CurlSurface:url-bad-port-demo-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-demo-1")]
    [SpecRequirement("CurlSurface:url-space-demo-1")]
    [DataRow("demo", UrlErrorChecks.BadPort)]
    [DataRow("demo", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("demo", UrlErrorChecks.Space)]
    public Task MalformedUrl_EndsWithExit3AndCurlsReason(string scheme, string check) => UrlErrorChecks.MalformedAsync(For(scheme), check);
}
