using Curl.Api.Codes;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class LibTestResultTests
{
    [TestMethod]
    public void Success_HasExitCodeZeroAndIsSuccess()
    {
        Assert.AreEqual(0, LibTestResult.Success.ExitCode);
        Assert.IsTrue(LibTestResult.Success.IsSuccess);
    }

    [TestMethod]
    public void IsSuccess_IsFalseForANonZeroExitCode()
    {
        Assert.IsFalse(new LibTestResult(120).IsSuccess);
    }

    [TestMethod]
    public void FromCurlCode_UsesTheNumberOfTheCode()
    {
        Assert.AreEqual(7, LibTestResult.FromCurlCode(CurlCode.CouldntConnect).ExitCode);
    }

    [TestMethod]
    public void FromCurlCode_OkIsSuccess()
    {
        Assert.AreEqual(LibTestResult.Success, LibTestResult.FromCurlCode(CurlCode.Ok));
    }
}
