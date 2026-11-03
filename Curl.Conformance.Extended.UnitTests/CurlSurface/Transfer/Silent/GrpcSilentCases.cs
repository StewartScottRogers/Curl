using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Silent;

/// <summary>curl's <c>-s</c> and <c>-S</c> (manual, "-s" and "-S") for <c>grpc</c>, <c>grpcs</c>.</summary>
[TestClass]
public sealed class GrpcSilentCases
{
    [TestMethod]
    [SpecRequirement("CurlSurface:silent-grpc-1")]
    [SpecRequirement("CurlSurface:silent-grpcs-1")]
    [DataRow("grpc")]
    [DataRow("grpcs")]
    public Task Silent_MalformedUrl_WritesNothingAndEndsWithExit3(string scheme) => SilentChecks.SilentMalformedAsync(GrpcUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:show-error-grpc-1")]
    [SpecRequirement("CurlSurface:show-error-grpcs-1")]
    [DataRow("grpc")]
    [DataRow("grpcs")]
    public Task ShowError_MalformedUrl_WritesOnlyTheErrorMessageAndEndsWithExit3(string scheme) => SilentChecks.ShowErrorMalformedAsync(GrpcUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:silent-refused-grpc-1")]
    [SpecRequirement("CurlSurface:silent-refused-grpcs-1")]
    [DataRow("grpc")]
    [DataRow("grpcs")]
    public Task Silent_ConnectionRefused_WritesNothingAndEndsWithExit7(string scheme) => SilentChecks.SilentRefusedAsync(GrpcUrlErrorCases.For(scheme));
}
