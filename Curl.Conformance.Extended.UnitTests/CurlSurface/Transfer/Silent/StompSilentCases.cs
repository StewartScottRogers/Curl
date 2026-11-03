using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Silent;

/// <summary>curl's <c>-s</c> and <c>-S</c> (manual, "-s" and "-S") for <c>stomp</c>, <c>stomps</c>.</summary>
[TestClass]
public sealed class StompSilentCases
{
    [TestMethod]
    [SpecRequirement("CurlSurface:silent-stomp-1")]
    [SpecRequirement("CurlSurface:silent-stomps-1")]
    [DataRow("stomp")]
    [DataRow("stomps")]
    public Task Silent_MalformedUrl_WritesNothingAndEndsWithExit3(string scheme) => SilentChecks.SilentMalformedAsync(StompUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:show-error-stomp-1")]
    [SpecRequirement("CurlSurface:show-error-stomps-1")]
    [DataRow("stomp")]
    [DataRow("stomps")]
    public Task ShowError_MalformedUrl_WritesOnlyTheErrorMessageAndEndsWithExit3(string scheme) => SilentChecks.ShowErrorMalformedAsync(StompUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:silent-refused-stomp-1")]
    [SpecRequirement("CurlSurface:silent-refused-stomps-1")]
    [DataRow("stomp")]
    [DataRow("stomps")]
    public Task Silent_ConnectionRefused_WritesNothingAndEndsWithExit7(string scheme) => SilentChecks.SilentRefusedAsync(StompUrlErrorCases.For(scheme));
}
