using System.Net;

namespace Sample.UnitTests;

// Fixture for Find-WeakTests.ps1 -SelfTest. Nothing here is real: test names that state a number
// as their outcome, the bodies that check it and the bodies that do not, and a commented-out test.
[TestClass]
public sealed class NumberedOutcomeTests
{
    // Weak: the name says exit code 9; the body checks 4.
    [TestMethod]
    public void Parse_DeepNesting_ExitsWithCode9()
    {
        int exitCode = 4;
        Assert.AreEqual(4, exitCode);
    }

    // Weak: the name says 404; the body checks 200.
    [TestMethod]
    public void Get_MissingPage_Returns404()
    {
        int status = 200;
        Assert.AreEqual(200, status);
    }

    // Sound: 404 by its HttpStatusCode member.
    [TestMethod]
    public void Get_MissingPage_ReturnsStatus404()
    {
        HttpStatusCode status = HttpStatusCode.NotFound;
        Assert.AreEqual(HttpStatusCode.NotFound, status);
    }

    // Sound: the exit-code check is left to a helper, which names 2.
    [TestMethod]
    public void Parse_UnknownOption_FailsWithExit2() => AssertRefused("--nope");

    // Sound: numbers glued to other words are names, and NotExit15 says what it does not end with.
    [TestMethod]
    public void Decode_Http2FrameWithSha256_EndsWithTheTimeoutNotExit15()
    {
        Assert.AreEqual(28, 28);
    }

    // Sound: "*/*" in a string opens no comment, so the test after it is not commented out.
    [TestMethod]
    public void Send_Get_AcceptsAnything()
    {
        Assert.AreEqual("*/*", "*/*");
    }

    [TestMethod]
    public void Send_Head_AcceptsAnything()
    {
        Assert.AreEqual("Accept: */*", "Accept: */*");
    }

    // Weak: the whole test is commented out.
    // [TestMethod]
    // public void Close_Twice_IsHarmless()
    // {
    //     Assert.IsTrue(true);
    // }

    private static void AssertRefused(string option)
    {
        int exitCode = option.Length > 0 ? 2 : 0;
        Assert.AreEqual(2, exitCode);
    }
}
