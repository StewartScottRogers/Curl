using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>curl's URL errors (manual, "EXIT CODES" 3, 6 and 7) for <c>az</c>, <c>abfs</c>, <c>abfss</c>.</summary>
[TestClass]
public sealed class AzureStorageUrlErrorCases
{
    internal static UrlErrorScheme For(string scheme) => scheme switch
    {
        "az" => new(scheme, Path: "/c/x", RefusedTarget: "localhost.blob.core.windows.net:443"),
        "abfs" => new(scheme, UserInfo: "fs@", RefusedTarget: "localhost.dfs.core.windows.net:443"),
        _ => new(scheme, UserInfo: "fs@", RefusedTarget: "localhost.dfs.core.windows.net:443"),
    };

    [TestMethod]
    [SpecRequirement("CurlSurface:url-bad-port-az-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-az-1")]
    [SpecRequirement("CurlSurface:url-space-az-1")]
    [SpecRequirement("CurlSurface:url-bad-port-abfs-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-abfs-1")]
    [SpecRequirement("CurlSurface:url-space-abfs-1")]
    [SpecRequirement("CurlSurface:url-bad-port-abfss-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-abfss-1")]
    [SpecRequirement("CurlSurface:url-space-abfss-1")]
    [DataRow("az", UrlErrorChecks.BadPort)]
    [DataRow("az", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("az", UrlErrorChecks.Space)]
    [DataRow("abfs", UrlErrorChecks.BadPort)]
    [DataRow("abfs", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("abfs", UrlErrorChecks.Space)]
    [DataRow("abfss", UrlErrorChecks.BadPort)]
    [DataRow("abfss", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("abfss", UrlErrorChecks.Space)]
    public Task MalformedUrl_EndsWithExit3AndCurlsReason(string scheme, string check) => UrlErrorChecks.MalformedAsync(For(scheme), check);

    [TestMethod]
    [SpecRequirement("CurlSurface:connection-refused-az-1")]
    [SpecRequirement("CurlSurface:connection-refused-abfs-1")]
    [SpecRequirement("CurlSurface:connection-refused-abfss-1")]
    [DataRow("az")]
    [DataRow("abfs")]
    [DataRow("abfss")]
    public Task ConnectionRefused_EndsWithExit7(string scheme) => UrlErrorChecks.ConnectionRefusedAsync(For(scheme));
}
