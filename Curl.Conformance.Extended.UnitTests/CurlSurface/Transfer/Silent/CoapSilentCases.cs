using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Silent;

/// <summary>curl's <c>-s</c> and <c>-S</c> (manual, "-s" and "-S") for <c>coap</c>, <c>coaps</c>.</summary>
[TestClass]
public sealed class CoapSilentCases
{
    [TestMethod]
    [SpecRequirement("CurlSurface:silent-coap-1")]
    [SpecRequirement("CurlSurface:silent-coaps-1")]
    [DataRow("coap")]
    [DataRow("coaps")]
    public Task Silent_MalformedUrl_WritesNothingAndEndsWithExit3(string scheme) => SilentChecks.SilentMalformedAsync(CoapUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:show-error-coap-1")]
    [SpecRequirement("CurlSurface:show-error-coaps-1")]
    [DataRow("coap")]
    [DataRow("coaps")]
    public Task ShowError_MalformedUrl_WritesOnlyTheErrorMessageAndEndsWithExit3(string scheme) => SilentChecks.ShowErrorMalformedAsync(CoapUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:silent-refused-coap-1")]
    [SpecRequirement("CurlSurface:silent-refused-coaps-1")]
    [DataRow("coap")]
    [DataRow("coaps")]
    public Task Silent_ConnectionRefused_WritesNothingAndEndsWithExit7(string scheme) => SilentChecks.SilentRefusedAsync(CoapUrlErrorCases.For(scheme));
}
