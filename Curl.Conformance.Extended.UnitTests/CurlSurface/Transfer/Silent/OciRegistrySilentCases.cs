using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Silent;

/// <summary>curl's <c>-s</c> and <c>-S</c> (manual, "-s" and "-S") for <c>oci</c>.</summary>
[TestClass]
public sealed class OciRegistrySilentCases
{
    [TestMethod]
    [SpecRequirement("CurlSurface:silent-oci-1")]
    [DataRow("oci")]
    public Task Silent_MalformedUrl_WritesNothingAndEndsWithExit3(string scheme) => SilentChecks.SilentMalformedAsync(OciRegistryUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:show-error-oci-1")]
    [DataRow("oci")]
    public Task ShowError_MalformedUrl_WritesOnlyTheErrorMessageAndEndsWithExit3(string scheme) => SilentChecks.ShowErrorMalformedAsync(OciRegistryUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:silent-refused-oci-1")]
    [DataRow("oci")]
    public Task Silent_ConnectionRefused_WritesNothingAndEndsWithExit7(string scheme) => SilentChecks.SilentRefusedAsync(OciRegistryUrlErrorCases.For(scheme));
}
