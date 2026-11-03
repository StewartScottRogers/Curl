using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Silent;

/// <summary>curl's <c>-s</c> and <c>-S</c> (manual, "-s" and "-S") for <c>s3</c>.</summary>
[TestClass]
public sealed class S3SilentCases
{
    [TestMethod]
    [SpecRequirement("CurlSurface:silent-s3-1")]
    [DataRow("s3")]
    public Task Silent_MalformedUrl_WritesNothingAndEndsWithExit3(string scheme) => SilentChecks.SilentMalformedAsync(S3UrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:show-error-s3-1")]
    [DataRow("s3")]
    public Task ShowError_MalformedUrl_WritesOnlyTheErrorMessageAndEndsWithExit3(string scheme) => SilentChecks.ShowErrorMalformedAsync(S3UrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:silent-refused-s3-1")]
    [DataRow("s3")]
    public Task Silent_ConnectionRefused_WritesNothingAndEndsWithExit7(string scheme) => SilentChecks.SilentRefusedAsync(S3UrlErrorCases.For(scheme));
}
