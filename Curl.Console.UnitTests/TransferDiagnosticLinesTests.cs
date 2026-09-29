using Curl.Cli;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins the runner's diagnostic log messages about each transfer (ADR-0222), and that none of them
/// carries a credential (decision 7).
/// </summary>
[TestClass]
public sealed class TransferDiagnosticLinesTests
{
    [TestMethod]
    public void Started_PlainGet_NamesSchemeHostDefaultMethodStandardOutputAndSwitchesOff()
    {
        string line = TransferDiagnosticLines.Started(0, OptionsOf("http://h.example/a"), "http://h.example/a", writesToFile: false);

        Assert.AreEqual(
            "transfer 0 started: scheme http, host h.example, method default, output standard output, verbose off, trace off, silent off",
            line);
    }

    [TestMethod]
    public void Started_MethodTraceSilentAndFile_NamesThem()
    {
        string line = TransferDiagnosticLines.Started(
            3,
            OptionsOf("-X", "PUT", "--trace-ascii", "t", "-s", "http://h/"),
            "http://h/",
            writesToFile: true);

        Assert.AreEqual(
            "transfer 3 started: scheme http, host h, method PUT, output file, verbose off, trace on, silent on",
            line);
    }

    [TestMethod]
    public void Started_Verbose_SaysVerboseOn()
    {
        string line = TransferDiagnosticLines.Started(0, OptionsOf("-v", "http://h/"), "http://h/", writesToFile: false);

        StringAssert.Contains(line, "verbose on, trace off");
    }

    [TestMethod]
    public void Started_UserOption_SaysCredentialsGivenWithoutThePassword()
    {
        string line = TransferDiagnosticLines.Started(0, OptionsOf("-u", "user:s3cret", "http://h/"), "http://h/", writesToFile: false);

        StringAssert.EndsWith(line, ", credentials given");
        Assert.IsFalse(line.Contains("s3cret", StringComparison.Ordinal));
        Assert.IsFalse(line.Contains("user", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Started_UrlUserInformation_SaysCredentialsGivenWithoutThePassword()
    {
        string line = TransferDiagnosticLines.Started(0, OptionsOf("http://u:s3cret@h/"), "http://u:s3cret@h/", writesToFile: false);

        StringAssert.Contains(line, "host h,");
        StringAssert.EndsWith(line, ", credentials given");
        Assert.IsFalse(line.Contains("s3cret", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Started_UrlThatDoesNotParse_SaysSoWithoutQuotingIt()
    {
        string line = TransferDiagnosticLines.Started(0, OptionsOf("http://h/"), "http://[bad/", writesToFile: false);

        StringAssert.StartsWith(line, "transfer 0 started: URL not parsed, method default");
    }

    [TestMethod]
    public void Ended_Success_GivesExitBytesAndElapsedMilliseconds()
    {
        string line = TransferDiagnosticLines.Ended(1, TransferResult.Success(5), TimeSpan.FromMilliseconds(12.7));

        Assert.AreEqual("transfer 1 ended: exit 0, 5 bytes, 12 ms", line);
    }

    [TestMethod]
    public void Ended_Failure_GivesItsExitCode()
    {
        string line = TransferDiagnosticLines.Ended(0, TransferResult.Failure(CurlExitCode.CouldntConnect, "no"), TimeSpan.Zero);

        Assert.AreEqual("transfer 0 ended: exit 7, 0 bytes, 0 ms", line);
    }

    [TestMethod]
    public void Failed_GivesTheExitCodeNumberAndName()
    {
        Assert.AreEqual("transfer 2 failed: exit 7 CouldntConnect", TransferDiagnosticLines.Failed(2, CurlExitCode.CouldntConnect));
    }

    private static CommandLineOptions OptionsOf(params string[] arguments) =>
        CommandLineParser.Parse(arguments, _ => true).Options!;
}
