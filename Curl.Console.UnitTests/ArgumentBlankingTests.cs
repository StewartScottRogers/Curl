using System.Text;
using Curl.Cli;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="NativeArgumentBlanking"/> (STD-048, BL-5093, ADR-1265): which argv positions hold a secret
/// curl's <c>cleanarg()</c> blanks, where they lie in argv's bytes, and that the run still parses the credentials.
/// </summary>
[TestClass]
public sealed class ArgumentBlankingTests
{
    [TestMethod]
    [DataRow("-u")]
    [DataRow("--user")]
    [DataRow("-U")]
    [DataRow("--proxy-user")]
    [DataRow("--oauth2-bearer")]
    [DataRow("--pass")]
    [DataRow("--proxy-pass")]
    [DataRow("-E")]
    [DataRow("--cert")]
    [DataRow("--proxy-cert")]
    public void SecretValues_SecretOptionWithSeparateValue_GivesTheValueWhole(string option)
    {
        IReadOnlyList<(int Index, int Start)> secrets = NativeArgumentBlanking.SecretValues(["-sS", option, "secret", "http://h/"]);

        CollectionAssert.AreEqual(new[] { (2, 0) }, secrets.ToArray());
    }

    [TestMethod]
    [DataRow("-ualice:pw", 2)]
    [DataRow("-Ubob:pw", 2)]
    [DataRow("-Ec.pem:pw", 2)]
    [DataRow("-sSualice:pw", 4)]
    [DataRow("--user=alice:pw", 7)]
    [DataRow("--oauth2-bearer=token", 16)]
    [DataRow("--proxy-cert=c.pem:pw", 13)]
    public void SecretValues_AttachedValue_GivesTheOffsetPastTheOption(string argument, int start)
    {
        IReadOnlyList<(int Index, int Start)> secrets = NativeArgumentBlanking.SecretValues(["http://h/", argument]);

        CollectionAssert.AreEqual(new[] { (1, start) }, secrets.ToArray());
    }

    [TestMethod]
    public void SecretValues_OtherOptions_AreUntouched()
    {
        IReadOnlyList<(int Index, int Start)> secrets = NativeArgumentBlanking.SecretValues(
            ["-H", "-u x", "--header=a", "-ofile", "--compressed", "-v", "-", "--no-such", "http://h/", "-:u", "-%u", "--unknown=1", "-x", "p", "-h"]);

        Assert.IsEmpty(secrets);
    }

    [TestMethod]
    public void SecretValues_ValueOfAnotherOption_IsNotReadAsAnOption()
    {
        IReadOnlyList<(int Index, int Start)> secrets = NativeArgumentBlanking.SecretValues(["-d", "-u", "x", "-o", "--pass"]);

        Assert.IsEmpty(secrets);
    }

    [TestMethod]
    public void SecretValues_AfterDoubleDash_StopsScanning()
    {
        IReadOnlyList<(int Index, int Start)> secrets = NativeArgumentBlanking.SecretValues(["--pass", "a", "--", "-u", "b"]);

        CollectionAssert.AreEqual(new[] { (1, 0) }, secrets.ToArray());
    }

    [TestMethod]
    public void SecretValues_SecretOptionLast_GivesNothing()
    {
        Assert.IsEmpty(NativeArgumentBlanking.SecretValues(["http://h/", "-u"]));
    }

    [TestMethod]
    public void SecretRanges_HostedByDotnet_LinesUpWithTheLastArguments()
    {
        byte[] commandLine = Encoding.UTF8.GetBytes("dotnet\0curl.dll\0-u\0alice:pw\0-ubob:x\0http://h/\0");

        IReadOnlyList<(int Offset, int Length)> ranges = NativeArgumentBlanking.SecretRanges(commandLine, ["-u", "alice:pw", "-ubob:x", "http://h/"]);

        CollectionAssert.AreEqual(new[] { (19, 8), (30, 5) }, ranges.ToArray());
    }

    [TestMethod]
    public void SecretRanges_EmptyAttachedValueAndLastStringWithoutNul_BlanksOnlyBytesThatAreThere()
    {
        byte[] commandLine = Encoding.UTF8.GetBytes("curl\0--user=\0--pass\0pé");

        IReadOnlyList<(int Offset, int Length)> ranges = NativeArgumentBlanking.SecretRanges(commandLine, ["--user=", "--pass", "pé"]);

        CollectionAssert.AreEqual(new[] { (20, 3) }, ranges.ToArray());
    }

    [TestMethod]
    public void SecretRanges_FewerStringsThanArguments_GivesNothing()
    {
        Assert.IsEmpty(NativeArgumentBlanking.SecretRanges("curl\0"u8, ["-u", "a:b"]));
    }

    [TestMethod]
    public void Blank_SecretRanges_OverwritesThemWithAsterisksOfTheSameLength()
    {
        byte[] commandLine = Encoding.UTF8.GetBytes("curl\0-sS\0-u\0alice:Secret1\0--oauth2-bearer=S6\0http://h/\0");
        string[] arguments = ["-sS", "-u", "alice:Secret1", "--oauth2-bearer=S6", "http://h/"];

        NativeArgumentBlanking.Blank(commandLine, NativeArgumentBlanking.SecretRanges(commandLine, arguments));

        Assert.AreEqual("curl\0-sS\0-u\0*************\0--oauth2-bearer=**\0http://h/\0", Encoding.UTF8.GetString(commandLine));
    }

    [TestMethod]
    public void Blank_ArgumentsReadBeforehand_StillParseTheCredentials()
    {
        byte[] commandLine = Encoding.UTF8.GetBytes("curl\0-u\0alice:Secret1\0--oauth2-bearer\0S6\0http://h/\0");
        IReadOnlyList<string> arguments = RawCommandLineArguments.Select(
            ["-u", "alice:Secret1", "--oauth2-bearer", "S6", "http://h/"],
            RawCommandLineArguments.SplitProcCommandLine(commandLine));

        NativeArgumentBlanking.Blank(commandLine, NativeArgumentBlanking.SecretRanges(commandLine, arguments));
        CommandLineOptions? options = CommandLineParser.Parse(arguments).Options;

        Assert.DoesNotContain("Secret1", Encoding.UTF8.GetString(commandLine));
        Assert.IsNotNull(options);
        Assert.AreEqual("Secret1", options.Credentials?.Password);
        Assert.AreEqual("S6", options.BearerToken);
    }

    [TestMethod]
    public void ArgumentAddresses_ProcSelfStatLine_GivesFields48And49()
    {
        string fields = string.Join(' ', Enumerable.Range(3, 50));

        Assert.AreEqual((48L, 49L), NativeArgumentBlanking.ArgumentAddresses($"1234 (curl ) x) {fields}"));
    }

    [TestMethod]
    public void ArgumentAddresses_TooFewFields_GivesNull()
    {
        Assert.IsNull(NativeArgumentBlanking.ArgumentAddresses("1234 (curl) 3 4 5"));
    }
}
