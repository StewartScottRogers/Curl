using Curl.Cli;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the runner's diagnostic log messages about each transfer (ADR-0222), and that none of them
/// carries a credential (decision 7).
/// </summary>
[TestClass]
public sealed class TransferDiagnosticLinesTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Started_PlainGet_NamesSchemeHostDefaultMethodStandardOutputAndSwitchesOff()
    {
        string line = Started(0, ["http://h.example/a"], "http://h.example/a", writesToFile: false);

        Diagnostics.Assert("line", "transfer 0 started: scheme http, host h.example, method default, output standard output, verbose off, trace off, silent off", line);
        Assert.AreEqual(
            "transfer 0 started: scheme http, host h.example, method default, output standard output, verbose off, trace off, silent off",
            line);
    }

    [TestMethod]
    public void Started_MethodTraceSilentAndFile_NamesThem()
    {
        string line = Started(3, ["-X", "PUT", "--trace-ascii", "t", "-s", "http://h/"], "http://h/", writesToFile: true);

        Diagnostics.Assert("line", "transfer 3 started: scheme http, host h, method PUT, output file, verbose off, trace on, silent on", line);
        Assert.AreEqual(
            "transfer 3 started: scheme http, host h, method PUT, output file, verbose off, trace on, silent on",
            line);
    }

    [TestMethod]
    public void Started_Verbose_SaysVerboseOn()
    {
        string line = Started(0, ["-v", "http://h/"], "http://h/", writesToFile: false);

        Diagnostics.Assert("line contains", "verbose on, trace off", line);
        StringAssert.Contains(line, "verbose on, trace off");
    }

    [TestMethod]
    public void Started_UserOption_SaysCredentialsGivenWithoutThePassword()
    {
        string line = Started(0, ["-u", "user:s3cret", "http://h/"], "http://h/", writesToFile: false);

        Diagnostics.Assert("line ends with, holding neither \"s3cret\" nor \"user\"", ", credentials given", line);
        StringAssert.EndsWith(line, ", credentials given");
        Assert.IsFalse(line.Contains("s3cret", StringComparison.Ordinal));
        Assert.IsFalse(line.Contains("user", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Started_UrlUserInformation_SaysCredentialsGivenWithoutThePassword()
    {
        string line = Started(0, ["http://u:s3cret@h/"], "http://u:s3cret@h/", writesToFile: false);

        Diagnostics.Assert("line holds \"host h,\", has no \"s3cret\" and ends with", ", credentials given", line);
        StringAssert.Contains(line, "host h,");
        StringAssert.EndsWith(line, ", credentials given");
        Assert.IsFalse(line.Contains("s3cret", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Started_UrlThatDoesNotParse_SaysSoWithoutQuotingIt()
    {
        string line = Started(0, ["http://h/"], "http://[bad/", writesToFile: false);

        Diagnostics.Assert("line starts with", "transfer 0 started: URL not parsed, method default", line);
        StringAssert.StartsWith(line, "transfer 0 started: URL not parsed, method default");
    }

    [TestMethod]
    public void Ended_Success_GivesExitBytesAndElapsedMilliseconds()
    {
        Diagnostics.Arrange("transfer / result / elapsed", "1 / success, 5 bytes / 12.7 ms");
        string line = TransferDiagnosticLines.Ended(1, TransferResult.Success(5), TimeSpan.FromMilliseconds(12.7));
        Diagnostics.Act("line", line);

        Diagnostics.Assert("line", "transfer 1 ended: exit 0, 5 bytes, 12 ms", line);
        Assert.AreEqual("transfer 1 ended: exit 0, 5 bytes, 12 ms", line);
    }

    [TestMethod]
    public void Ended_Failure_GivesItsExitCode()
    {
        Diagnostics.Arrange("transfer / result / elapsed", "0 / failure CouldntConnect / 0 ms");
        string line = TransferDiagnosticLines.Ended(0, TransferResult.Failure(CurlExitCode.CouldntConnect, "no"), TimeSpan.Zero);
        Diagnostics.Act("line", line);

        Diagnostics.Assert("line", "transfer 0 ended: exit 7, 0 bytes, 0 ms", line);
        Assert.AreEqual("transfer 0 ended: exit 7, 0 bytes, 0 ms", line);
    }

    [TestMethod]
    public void Failed_GivesTheExitCodeNumberAndName()
    {
        Diagnostics.Arrange("transfer / exit code", "2 / CouldntConnect");
        Diagnostics.Act("line", TransferDiagnosticLines.Failed(2, CurlExitCode.CouldntConnect));

        Diagnostics.Assert("line", "transfer 2 failed: exit 7 CouldntConnect", TransferDiagnosticLines.Failed(2, CurlExitCode.CouldntConnect));
        Assert.AreEqual("transfer 2 failed: exit 7 CouldntConnect", TransferDiagnosticLines.Failed(2, CurlExitCode.CouldntConnect));
    }

    private string Started(int index, string[] arguments, string url, bool writesToFile)
    {
        Diagnostics.Arrange("command line / URL / writes to file", $"{string.Join(' ', arguments)} / {url} / {writesToFile}");
        string line = TransferDiagnosticLines.Started(index, OptionsOf(arguments), url, writesToFile);
        Diagnostics.Act("line", line);
        return line;
    }

    private static CommandLineOptions OptionsOf(params string[] arguments) =>
        CommandLineParser.Parse(arguments, _ => true).Options!;
}
