using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Silent;

/// <summary>curl's <c>-s</c> and <c>-S</c> (manual, "-s" and "-S") for <c>redis</c>, <c>rediss</c>.</summary>
[TestClass]
public sealed class RedisSilentCases
{
    [TestMethod]
    [SpecRequirement("CurlSurface:silent-redis-1")]
    [SpecRequirement("CurlSurface:silent-rediss-1")]
    [DataRow("redis")]
    [DataRow("rediss")]
    public Task Silent_MalformedUrl_WritesNothingAndEndsWithExit3(string scheme) => SilentChecks.SilentMalformedAsync(RedisUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:show-error-redis-1")]
    [SpecRequirement("CurlSurface:show-error-rediss-1")]
    [DataRow("redis")]
    [DataRow("rediss")]
    public Task ShowError_MalformedUrl_WritesOnlyTheErrorMessageAndEndsWithExit3(string scheme) => SilentChecks.ShowErrorMalformedAsync(RedisUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:silent-refused-redis-1")]
    [SpecRequirement("CurlSurface:silent-refused-rediss-1")]
    [DataRow("redis")]
    [DataRow("rediss")]
    public Task Silent_ConnectionRefused_WritesNothingAndEndsWithExit7(string scheme) => SilentChecks.SilentRefusedAsync(RedisUrlErrorCases.For(scheme));
}
