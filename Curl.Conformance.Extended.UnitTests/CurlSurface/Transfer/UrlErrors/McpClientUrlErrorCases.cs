using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>curl's URL errors (manual, "EXIT CODES" 3, 6 and 7) for <c>mcp</c>, <c>mcps</c>, <c>mcp+stdio</c>.</summary>
[TestClass]
public sealed class McpClientUrlErrorCases
{
    internal static UrlErrorScheme For(string scheme) => scheme switch
    {
        "mcp" => new(scheme),
        "mcps" => new(scheme),
        _ => new(scheme),
    };

    [TestMethod]
    [SpecRequirement("CurlSurface:url-bad-port-mcp-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-mcp-1")]
    [SpecRequirement("CurlSurface:url-space-mcp-1")]
    [SpecRequirement("CurlSurface:url-bad-port-mcps-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-mcps-1")]
    [SpecRequirement("CurlSurface:url-space-mcps-1")]
    [SpecRequirement("CurlSurface:url-bad-port-mcp+stdio-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-mcp+stdio-1")]
    [SpecRequirement("CurlSurface:url-space-mcp+stdio-1")]
    [DataRow("mcp", UrlErrorChecks.BadPort)]
    [DataRow("mcp", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("mcp", UrlErrorChecks.Space)]
    [DataRow("mcps", UrlErrorChecks.BadPort)]
    [DataRow("mcps", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("mcps", UrlErrorChecks.Space)]
    [DataRow("mcp+stdio", UrlErrorChecks.BadPort)]
    [DataRow("mcp+stdio", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("mcp+stdio", UrlErrorChecks.Space)]
    public Task MalformedUrl_EndsWithExit3AndCurlsReason(string scheme, string check) => UrlErrorChecks.MalformedAsync(For(scheme), check);

    [TestMethod]
    [SpecRequirement("CurlSurface:unresolvable-host-mcp-1")]
    [SpecRequirement("CurlSurface:unresolvable-host-mcps-1")]
    [DataRow("mcp")]
    [DataRow("mcps")]
    public Task UnresolvableHost_EndsWithExit6(string scheme) => UrlErrorChecks.UnresolvableHostAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:connection-refused-mcp-1")]
    [SpecRequirement("CurlSurface:connection-refused-mcps-1")]
    [DataRow("mcp")]
    [DataRow("mcps")]
    public Task ConnectionRefused_EndsWithExit7(string scheme) => UrlErrorChecks.ConnectionRefusedAsync(For(scheme));
}
