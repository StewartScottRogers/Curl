namespace Sample.UnitTests;

// Fixture for Find-WeakTests.ps1 -SelfTest (BL-1368). Nothing here is real: six weak tests, one
// of each kind the audit seeder plants, and five sound ones the scan must leave alone.
[TestClass]
public sealed class SampleTests
{
    // Weak: the exact log line's AreEqual was replaced by IsNotNull (as PD-001 planted).
    [TestMethod]
    public void Write_Info_WritesTheLine()
    {
        var output = new System.Text.StringBuilder("x");
        Assert.IsNotNull(output.ToString());
    }

    // Weak: renamed to promise another exception; the body still checks ArgumentException.
    [TestMethod]
    public void Constructor_TwoHandlers_ThrowsInvalidOperationExceptionNamingDict()
    {
        Assert.ThrowsExactly<System.ArgumentException>(() => throw new System.ArgumentException("dict"));
    }

    // Weak: renamed to promise exit 23; the body still checks ReadError (26).
    [TestMethod]
    public void RunAsync_SaveFails_ExitsWith23()
    {
        var code = CurlExitCode.ReadError;
        Assert.AreEqual(CurlExitCode.ReadError, code);
    }

    // Weak: its only assertion is commented out.
    [TestMethod]
    public void Refusal_AfterExpiry_HasExpired()
    {
        var expired = true;
        // Assert.IsTrue(expired);
        _ = expired;
    }

    // Weak: ignored.
    [Ignore]
    [TestMethod]
    public void Retry_WithoutMaxTime_IsNeverAbandoned()
    {
        Assert.AreEqual(1, 1);
    }

    // Weak: renamed to promise FormatException; the body still checks ArgumentNullException.
    [TestMethod]
    public void Parse_Value_ThrowsFormatException()
    {
        Assert.ThrowsExactly<System.ArgumentNullException>(() => throw new System.ArgumentNullException("value"));
    }

    // Sound: exit 7 by its member name.
    [TestMethod]
    public void Connect_Refused_ExitsWith7()
    {
        Assert.AreEqual(CurlExitCode.CouldntConnect, CurlExitCode.CouldntConnect);
    }

    // Sound: the camel-case word after Throws is in the body.
    [TestMethod]
    public void Parse_Null_ThrowsArgumentNull()
    {
        Assert.ThrowsExactly<System.ArgumentNullException>(() => throw new System.ArgumentNullException("value"));
    }

    // Sound: an exact check; "// not a comment" in a string is kept.
    [TestMethod]
    public void Format_Url_KeepsTheSlashes()
    {
        Assert.AreEqual("http://h/", "http://h/");
    }

    // Sound: the expected exception is the assertion.
    [TestMethod]
    [ExpectedException(typeof(System.InvalidOperationException))]
    public void Run_Twice_Refuses()
    {
        throw new System.InvalidOperationException();
    }

    // Sound: a helper named Assert... is an assertion.
    [TestMethod]
    public void Write_Line_WritesIt()
    {
        AssertWrites("line");
    }

    private static void AssertWrites(string expected) => Assert.AreEqual(expected, expected);
}
