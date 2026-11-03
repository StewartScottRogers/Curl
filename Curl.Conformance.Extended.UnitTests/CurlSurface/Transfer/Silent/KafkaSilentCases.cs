using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Silent;

/// <summary>curl's <c>-s</c> and <c>-S</c> (manual, "-s" and "-S") for <c>kafka</c>, <c>kafkas</c>.</summary>
[TestClass]
public sealed class KafkaSilentCases
{
    [TestMethod]
    [SpecRequirement("CurlSurface:silent-kafka-1")]
    [SpecRequirement("CurlSurface:silent-kafkas-1")]
    [DataRow("kafka")]
    [DataRow("kafkas")]
    public Task Silent_MalformedUrl_WritesNothingAndEndsWithExit3(string scheme) => SilentChecks.SilentMalformedAsync(KafkaUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:show-error-kafka-1")]
    [SpecRequirement("CurlSurface:show-error-kafkas-1")]
    [DataRow("kafka")]
    [DataRow("kafkas")]
    public Task ShowError_MalformedUrl_WritesOnlyTheErrorMessageAndEndsWithExit3(string scheme) => SilentChecks.ShowErrorMalformedAsync(KafkaUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:silent-refused-kafka-1")]
    [SpecRequirement("CurlSurface:silent-refused-kafkas-1")]
    [DataRow("kafka")]
    [DataRow("kafkas")]
    public Task Silent_ConnectionRefused_WritesNothingAndEndsWithExit7(string scheme) => SilentChecks.SilentRefusedAsync(KafkaUrlErrorCases.For(scheme));
}
