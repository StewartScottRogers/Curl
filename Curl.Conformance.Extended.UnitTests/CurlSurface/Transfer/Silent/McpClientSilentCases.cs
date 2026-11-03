using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Silent;

/// <summary>curl's <c>-s</c> and <c>-S</c> (manual, "-s" and "-S") for <c>mcp</c>, <c>mcps</c>, <c>mcp+stdio</c>.</summary>
[TestClass]
public sealed class McpClientSilentCases
{
    [TestMethod]
    [SpecRequirement("CurlSurface:silent-mcp-1")]
    [SpecRequirement("CurlSurface:silent-mcps-1")]
    [SpecRequirement("CurlSurface:silent-mcp+stdio-1")]
    [DataRow("mcp")]
    [DataRow("mcps")]
    [DataRow("mcp+stdio")]
    public Task Silent_MalformedUrl_WritesNothingAndEndsWithExit3(string scheme) => SilentChecks.SilentMalformedAsync(McpClientUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:show-error-mcp-1")]
    [SpecRequirement("CurlSurface:show-error-mcps-1")]
    [SpecRequirement("CurlSurface:show-error-mcp+stdio-1")]
    [DataRow("mcp")]
    [DataRow("mcps")]
    [DataRow("mcp+stdio")]
    public Task ShowError_MalformedUrl_WritesOnlyTheErrorMessageAndEndsWithExit3(string scheme) => SilentChecks.ShowErrorMalformedAsync(McpClientUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:silent-refused-mcp-1")]
    [SpecRequirement("CurlSurface:silent-refused-mcps-1")]
    [DataRow("mcp")]
    [DataRow("mcps")]
    public Task Silent_ConnectionRefused_WritesNothingAndEndsWithExit7(string scheme) => SilentChecks.SilentRefusedAsync(McpClientUrlErrorCases.For(scheme));
}
