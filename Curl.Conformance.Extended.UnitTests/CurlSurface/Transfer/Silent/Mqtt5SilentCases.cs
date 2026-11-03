using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Silent;

/// <summary>curl's <c>-s</c> and <c>-S</c> (manual, "-s" and "-S") for <c>mqtt5</c>, <c>mqtt5s</c>, <c>mqtt5+ws</c>.</summary>
[TestClass]
public sealed class Mqtt5SilentCases
{
    [TestMethod]
    [SpecRequirement("CurlSurface:silent-mqtt5-1")]
    [SpecRequirement("CurlSurface:silent-mqtt5s-1")]
    [SpecRequirement("CurlSurface:silent-mqtt5+ws-1")]
    [DataRow("mqtt5")]
    [DataRow("mqtt5s")]
    [DataRow("mqtt5+ws")]
    public Task Silent_MalformedUrl_WritesNothingAndEndsWithExit3(string scheme) => SilentChecks.SilentMalformedAsync(Mqtt5UrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:show-error-mqtt5-1")]
    [SpecRequirement("CurlSurface:show-error-mqtt5s-1")]
    [SpecRequirement("CurlSurface:show-error-mqtt5+ws-1")]
    [DataRow("mqtt5")]
    [DataRow("mqtt5s")]
    [DataRow("mqtt5+ws")]
    public Task ShowError_MalformedUrl_WritesOnlyTheErrorMessageAndEndsWithExit3(string scheme) => SilentChecks.ShowErrorMalformedAsync(Mqtt5UrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:silent-refused-mqtt5-1")]
    [SpecRequirement("CurlSurface:silent-refused-mqtt5s-1")]
    [SpecRequirement("CurlSurface:silent-refused-mqtt5+ws-1")]
    [DataRow("mqtt5")]
    [DataRow("mqtt5s")]
    [DataRow("mqtt5+ws")]
    public Task Silent_ConnectionRefused_WritesNothingAndEndsWithExit7(string scheme) => SilentChecks.SilentRefusedAsync(Mqtt5UrlErrorCases.For(scheme));
}
