using Curl.Api.Codes;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class LibTestAssertionsTests
{
    [TestMethod]
    public void FailUnlessAndAbortUnless_OnAHoldingCondition_CountNothing()
    {
        var error = new StringWriter();
        var check = new LibTestAssertions(error);

        bool failHeld = check.FailUnless(true, "never");
        bool abortHeld = check.AbortUnless(true, "never");

        Assert.IsTrue(failHeld);
        Assert.IsTrue(abortHeld);
        Assert.AreEqual(0, check.Failures);
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public void EachCheck_OnAFailure_WritesUnitcheckLineAndCountsIt()
    {
        var error = new StringWriter();
        var check = new LibTestAssertions(error);
        int value = 2;

        bool failHeld = check.FailUnless(value == 3, "wrong value");
        bool abortHeld = check.AbortUnless(false, "returned NULL!", "out");
        check.Fail("unit_setup() FAILURE");

        Assert.IsFalse(failHeld);
        Assert.IsFalse(abortHeld);
        Assert.AreEqual(3, check.Failures);
        Assert.AreEqual((CurlCode)3, check.FailuresAsResult);
        string[] lines = error.ToString().Split('\n');
        StringAssert.StartsWith(lines[0], "LibTestAssertionsTests.cs:");
        StringAssert.EndsWith(lines[0], " Assertion 'value == 3' FAILED: wrong value");
        StringAssert.EndsWith(lines[1], " ABORT assertion 'out' failed: returned NULL!");
        StringAssert.EndsWith(lines[2], " test FAILED: 'unit_setup() FAILURE'");
    }

    [TestMethod]
    public void Constructor_WithoutAWriter_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new LibTestAssertions(null!));
    }
}
