using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Silent;

/// <summary>curl's <c>-s</c> and <c>-S</c> (manual, "-s" and "-S") for <c>signalr</c>, <c>signalrs</c>.</summary>
[TestClass]
public sealed class SignalRSilentCases
{
    [TestMethod]
    [SpecRequirement("CurlSurface:silent-signalr-1")]
    [SpecRequirement("CurlSurface:silent-signalrs-1")]
    [DataRow("signalr")]
    [DataRow("signalrs")]
    public Task Silent_MalformedUrl_WritesNothingAndEndsWithExit3(string scheme) => SilentChecks.SilentMalformedAsync(SignalRUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:show-error-signalr-1")]
    [SpecRequirement("CurlSurface:show-error-signalrs-1")]
    [DataRow("signalr")]
    [DataRow("signalrs")]
    public Task ShowError_MalformedUrl_WritesOnlyTheErrorMessageAndEndsWithExit3(string scheme) => SilentChecks.ShowErrorMalformedAsync(SignalRUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:silent-refused-signalr-1")]
    [SpecRequirement("CurlSurface:silent-refused-signalrs-1")]
    [DataRow("signalr")]
    [DataRow("signalrs")]
    public Task Silent_ConnectionRefused_WritesNothingAndEndsWithExit7(string scheme) => SilentChecks.SilentRefusedAsync(SignalRUrlErrorCases.For(scheme));
}
