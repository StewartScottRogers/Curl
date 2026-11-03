using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Silent;

/// <summary>curl's <c>-s</c> and <c>-S</c> (manual, "-s" and "-S") for <c>socketio</c>, <c>socketios</c>.</summary>
[TestClass]
public sealed class SocketIoSilentCases
{
    [TestMethod]
    [SpecRequirement("CurlSurface:silent-socketio-1")]
    [SpecRequirement("CurlSurface:silent-socketios-1")]
    [DataRow("socketio")]
    [DataRow("socketios")]
    public Task Silent_MalformedUrl_WritesNothingAndEndsWithExit3(string scheme) => SilentChecks.SilentMalformedAsync(SocketIoUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:show-error-socketio-1")]
    [SpecRequirement("CurlSurface:show-error-socketios-1")]
    [DataRow("socketio")]
    [DataRow("socketios")]
    public Task ShowError_MalformedUrl_WritesOnlyTheErrorMessageAndEndsWithExit3(string scheme) => SilentChecks.ShowErrorMalformedAsync(SocketIoUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:silent-refused-socketio-1")]
    [SpecRequirement("CurlSurface:silent-refused-socketios-1")]
    [DataRow("socketio")]
    [DataRow("socketios")]
    public Task Silent_ConnectionRefused_WritesNothingAndEndsWithExit7(string scheme) => SilentChecks.SilentRefusedAsync(SocketIoUrlErrorCases.For(scheme));
}
