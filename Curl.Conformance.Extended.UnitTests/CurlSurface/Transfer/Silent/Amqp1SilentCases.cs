using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Silent;

/// <summary>curl's <c>-s</c> and <c>-S</c> (manual, "-s" and "-S") for <c>amqp1</c>, <c>amqp1s</c>.</summary>
[TestClass]
public sealed class Amqp1SilentCases
{
    [TestMethod]
    [SpecRequirement("CurlSurface:silent-amqp1-1")]
    [SpecRequirement("CurlSurface:silent-amqp1s-1")]
    [DataRow("amqp1")]
    [DataRow("amqp1s")]
    public Task Silent_MalformedUrl_WritesNothingAndEndsWithExit3(string scheme) => SilentChecks.SilentMalformedAsync(Amqp1UrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:show-error-amqp1-1")]
    [SpecRequirement("CurlSurface:show-error-amqp1s-1")]
    [DataRow("amqp1")]
    [DataRow("amqp1s")]
    public Task ShowError_MalformedUrl_WritesOnlyTheErrorMessageAndEndsWithExit3(string scheme) => SilentChecks.ShowErrorMalformedAsync(Amqp1UrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:silent-refused-amqp1-1")]
    [SpecRequirement("CurlSurface:silent-refused-amqp1s-1")]
    [DataRow("amqp1")]
    [DataRow("amqp1s")]
    public Task Silent_ConnectionRefused_WritesNothingAndEndsWithExit7(string scheme) => SilentChecks.SilentRefusedAsync(Amqp1UrlErrorCases.For(scheme));
}
