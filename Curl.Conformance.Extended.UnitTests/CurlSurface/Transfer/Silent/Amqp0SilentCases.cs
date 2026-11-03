using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Silent;

/// <summary>curl's <c>-s</c> and <c>-S</c> (manual, "-s" and "-S") for <c>amqp</c>, <c>amqps</c>.</summary>
[TestClass]
public sealed class Amqp0SilentCases
{
    [TestMethod]
    [SpecRequirement("CurlSurface:silent-amqp-1")]
    [SpecRequirement("CurlSurface:silent-amqps-1")]
    [DataRow("amqp")]
    [DataRow("amqps")]
    public Task Silent_MalformedUrl_WritesNothingAndEndsWithExit3(string scheme) => SilentChecks.SilentMalformedAsync(Amqp0UrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:show-error-amqp-1")]
    [SpecRequirement("CurlSurface:show-error-amqps-1")]
    [DataRow("amqp")]
    [DataRow("amqps")]
    public Task ShowError_MalformedUrl_WritesOnlyTheErrorMessageAndEndsWithExit3(string scheme) => SilentChecks.ShowErrorMalformedAsync(Amqp0UrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:silent-refused-amqp-1")]
    [SpecRequirement("CurlSurface:silent-refused-amqps-1")]
    [DataRow("amqp")]
    [DataRow("amqps")]
    public Task Silent_ConnectionRefused_WritesNothingAndEndsWithExit7(string scheme) => SilentChecks.SilentRefusedAsync(Amqp0UrlErrorCases.For(scheme));
}
