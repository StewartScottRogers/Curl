using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Silent;

/// <summary>curl's <c>-s</c> and <c>-S</c> (manual, "-s" and "-S") for <c>nats</c>, <c>natss</c>.</summary>
[TestClass]
public sealed class NatsSilentCases
{
    [TestMethod]
    [SpecRequirement("CurlSurface:silent-nats-1")]
    [SpecRequirement("CurlSurface:silent-natss-1")]
    [DataRow("nats")]
    [DataRow("natss")]
    public Task Silent_MalformedUrl_WritesNothingAndEndsWithExit3(string scheme) => SilentChecks.SilentMalformedAsync(NatsUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:show-error-nats-1")]
    [SpecRequirement("CurlSurface:show-error-natss-1")]
    [DataRow("nats")]
    [DataRow("natss")]
    public Task ShowError_MalformedUrl_WritesOnlyTheErrorMessageAndEndsWithExit3(string scheme) => SilentChecks.ShowErrorMalformedAsync(NatsUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:silent-refused-nats-1")]
    [SpecRequirement("CurlSurface:silent-refused-natss-1")]
    [DataRow("nats")]
    [DataRow("natss")]
    public Task Silent_ConnectionRefused_WritesNothingAndEndsWithExit7(string scheme) => SilentChecks.SilentRefusedAsync(NatsUrlErrorCases.For(scheme));
}
