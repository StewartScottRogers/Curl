using Curl.Api.Codes;
using Curl.Protocol.Abstractions;

namespace Curl.Api.UnitTests.Codes;

[TestClass]
public sealed class CurlCodeConversionTests
{
    [TestMethod]
    public void ToCurlCode_EveryExitCode_KeepsTheNumberAndName()
    {
        foreach (var exitCode in Enum.GetValues<CurlExitCode>())
        {
            var code = exitCode.ToCurlCode();

            Assert.AreEqual((int)exitCode, (int)code);
            Assert.AreEqual(exitCode.ToString(), code.ToString());
        }
    }

    [TestMethod]
    public void ToCurlExitCode_EveryCurlCode_KeepsTheNumberAndRoundTrips()
    {
        foreach (var code in Enum.GetValues<CurlCode>())
        {
            var exitCode = code.ToCurlExitCode();

            Assert.AreEqual((int)code, (int)exitCode);
            Assert.AreEqual(code, exitCode.ToCurlCode());
        }
    }
}
