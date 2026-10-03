namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="CurlTimerOptions"/>: which command lines name a curl timer that races the
/// server, long or short, alone or bundled.
/// </summary>
[TestClass]
public sealed class CurlTimerOptionsTests
{
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
        Assert.IsTrue(CurlTimerOptions.AnyIn(["http://127.0.0.1/1", option, "2"]));
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
        Assert.IsFalse(CurlTimerOptions.AnyIn(["--include", argument]));
    }

    [TestMethod]
    public void AnyIn_NoArguments_IsFalse()
    {
        Assert.IsFalse(CurlTimerOptions.AnyIn([]));
    }

    [TestMethod]
    public void AnyIn_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlTimerOptions.AnyIn(null!));
    }
}
