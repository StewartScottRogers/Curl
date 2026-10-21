namespace Curl.Conformance.UnitProgram.UnitTests;

[TestClass]
public sealed class UnitProgramResultTests
{
    [TestMethod]
    public void Success_HasNoFailuresAndExitCodeZero()
    {
        Assert.AreEqual(0, UnitProgramResult.Success.FailureCount);
        Assert.AreEqual(0, UnitProgramResult.Success.ExitCode);
    }

    [TestMethod]
    public void ExitCode_IsTheFailureCount()
    {
        Assert.AreEqual(3, new UnitProgramResult(3).ExitCode);
    }
}
