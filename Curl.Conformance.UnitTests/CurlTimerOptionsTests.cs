using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="CurlTimerOptions"/>: which command lines name a curl timer that races the
/// server, long or short, alone or bundled.
/// </summary>
[TestClass]
public sealed class CurlTimerOptionsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("--max-time")]
    [DataRow("--connect-timeout")]
    [DataRow("--speed-time")]
    [DataRow("--speed-limit")]
    [DataRow("--expect100-timeout")]
    [DataRow("-m")]
    [DataRow("-y")]
    [DataRow("-Y")]
    [DataRow("-sm")]
    [DataRow("-m2")]
    public void AnyIn_ATimerOption_IsTrue(string option)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("option", option);

        bool actual = CurlTimerOptions.AnyIn(["http://127.0.0.1/1", option, "2"]);

        diagnostics.Act("any timer option", actual);
        diagnostics.Assert("any timer option", true, actual);
        Assert.IsTrue(actual);
    }

    [TestMethod]
    [DataRow("--data")]
    [DataRow("--max-filesize")]
    [DataRow("-d")]
    [DataRow("-")]
    [DataRow("my-file")]
    [DataRow("http://127.0.0.1/my")]
    public void AnyIn_NoTimerOption_IsFalse(string argument)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("argument", argument);

        bool actual = CurlTimerOptions.AnyIn(["--include", argument]);

        diagnostics.Act("any timer option", actual);
        diagnostics.Assert("any timer option", false, actual);
        Assert.IsFalse(actual);
    }

    [TestMethod]
    public void AnyIn_NoArguments_IsFalse()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("arguments", "(none)");

        bool actual = CurlTimerOptions.AnyIn([]);

        diagnostics.Act("any timer option", actual);
        diagnostics.Assert("any timer option", false, actual);
        Assert.IsFalse(actual);
    }

    [TestMethod]
    public void AnyIn_Null_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("arguments", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => CurlTimerOptions.AnyIn(null!));

        diagnostics.Act("exception", exception.Message);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }
}
