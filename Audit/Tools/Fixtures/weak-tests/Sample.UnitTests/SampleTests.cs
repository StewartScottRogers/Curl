namespace Sample.UnitTests;

// Fixture for Find-WeakTests.ps1 -SelfTest (BL-1368). Nothing here is real: weak tests of
// each kind the audit seeder plants, and sound ones the scan must leave alone.
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

    // Weak: the exact bytes' AreEqual became IsNotNull beside a kept exit-code check (PD-001 in a
    // test with other assertions).
    [TestMethod]
    public void Encode_Header_WritesTheBytes()
    {
        var exit = 0;
        var bytes = new byte[] { 1, 2 };
        Assert.AreEqual(0, exit);
        Assert.IsNotNull(bytes);
    }

    // Sound: IsNotNull guards a value the next assertion checks exactly.
    [TestMethod]
    public void Parse_Header_ReadsTheName()
    {
        var header = new System.Collections.Generic.KeyValuePair<string, string>("Host", "h");
        Assert.IsNotNull(header.Key);
        Assert.AreEqual("Host", header.Key);
    }

    // Weak: IsNotNull of a ToString call, which is never null, beside an exact check.
    [TestMethod]
    public void Format_Count_WritesTheDigits()
    {
        var count = 3;
        Assert.AreEqual(3, count);
        Assert.IsNotNull(count.ToString());
    }

    // Weak: the exit code's assertion is commented out while another assertion remains (PD-003).
    [TestMethod]
    public void Transfer_Timeout_SetsTheCode()
    {
        var code = 28;
        var tries = 2;
        Assert.AreEqual(2, tries);
        // Assert.AreEqual(28, code);
        _ = code;
    }

    // Weak: a fake returns an exact value, and the variable that should carry it is checked only
    // with IsNotNull beside a kept IsTrue (as audit 20261102-172448 missed one).
    [TestMethod]
    public void Rewrite_EmptyPath_ReadsTheConfiguredFile()
    {
        System.Func<string, string?> read = path => path == "/home/u/gateway" ? "http://127.0.0.1:9" : null;
        bool rewritten = Rewrite(read, out string? url);
        Assert.IsTrue(rewritten);
        Assert.IsNotNull(url);
    }

    // Sound: the same fake, and the exact value it returns is asserted inside the expected URL.
    [TestMethod]
    public void Rewrite_EmptyPath_UsesTheConfiguredGateway()
    {
        System.Func<string, string?> read = path => path == "/home/u/gateway" ? "http://127.0.0.1:9" : null;
        bool rewritten = Rewrite(read, out string? url);
        Assert.IsTrue(rewritten);
        Assert.AreEqual("http://127.0.0.1:9/x", url);
    }

    private static bool Rewrite(System.Func<string, string?> read, out string? url)
    {
        url = read("/home/u/gateway") + "/x";
        return true;
    }

    private static void AssertWrites(string expected) => Assert.AreEqual(expected, expected);
}
