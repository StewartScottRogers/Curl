using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Silent;

/// <summary>curl's <c>-s</c> and <c>-S</c> (manual, "-s" and "-S") for <c>az</c>, <c>abfs</c>, <c>abfss</c>.</summary>
[TestClass]
public sealed class AzureStorageSilentCases
{
    [TestMethod]
    [SpecRequirement("CurlSurface:silent-az-1")]
    [SpecRequirement("CurlSurface:silent-abfs-1")]
    [SpecRequirement("CurlSurface:silent-abfss-1")]
    [DataRow("az")]
    [DataRow("abfs")]
    [DataRow("abfss")]
    public Task Silent_MalformedUrl_WritesNothingAndEndsWithExit3(string scheme) => SilentChecks.SilentMalformedAsync(AzureStorageUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:show-error-az-1")]
    [SpecRequirement("CurlSurface:show-error-abfs-1")]
    [SpecRequirement("CurlSurface:show-error-abfss-1")]
    [DataRow("az")]
    [DataRow("abfs")]
    [DataRow("abfss")]
    public Task ShowError_MalformedUrl_WritesOnlyTheErrorMessageAndEndsWithExit3(string scheme) => SilentChecks.ShowErrorMalformedAsync(AzureStorageUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:silent-refused-az-1")]
    [SpecRequirement("CurlSurface:silent-refused-abfs-1")]
    [SpecRequirement("CurlSurface:silent-refused-abfss-1")]
    [DataRow("az")]
    [DataRow("abfs")]
    [DataRow("abfss")]
    public Task Silent_ConnectionRefused_WritesNothingAndEndsWithExit7(string scheme) => SilentChecks.SilentRefusedAsync(AzureStorageUrlErrorCases.For(scheme));
}
