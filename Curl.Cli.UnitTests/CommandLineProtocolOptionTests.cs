using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records the Phase 4 protocol options under their ADR-0006 names:
/// <c>-d</c>/<c>--data</c> as UTF-8 <see cref="CommandLineOptions.PostData"/>,
/// <c>-u</c>/<c>--user</c> split at the first colon (a value with no colon is pinned in
/// <see cref="CommandLinePasswordPromptTests"/>), every <c>-t</c>/<c>--telnet-option</c>
/// verbatim and in order, <c>--tftp-blksize</c> unclamped, and <c>--tftp-no-options</c>.
/// Refusal lines were measured against the local curl 8.21.0 on 2026-09-26.
/// </summary>
[TestClass]
public sealed class CommandLineProtocolOptionTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("-d")]
    [DataRow("--data")]
    public void Parse_Data_RecordsUtf8Bytes(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, "75", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        AssertPostData(new byte[] { 0x37, 0x35 }, result);
        CollectionAssert.AreEqual(new byte[] { 0x37, 0x35 }, result.Options.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_DataWithNonAsciiText_RecordsUtf8Bytes()
    {
        CommandLineParseResult result = Parse(["-d", "é", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        AssertPostData(new byte[] { 0xC3, 0xA9 }, result);
        CollectionAssert.AreEqual(new byte[] { 0xC3, 0xA9 }, result.Options.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_EmptyData_RecordsEmptyData()
    {
        CommandLineParseResult result = Parse(["--data=", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        AssertPostData([], result);
        Assert.IsTrue(result.Options.PostData.HasValue);
        Assert.AreEqual(0, result.Options.PostData.Value.Length);
    }

    [TestMethod]
    public void Parse_DataGivenTwice_JoinsThemWithAnAmpersand()
    {
        CommandLineParseResult result = Parse(["-d", "a", "-d", "b", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        AssertPostData(new byte[] { 0x61, 0x26, 0x62 }, result);
        CollectionAssert.AreEqual(new byte[] { 0x61, 0x26, 0x62 }, result.Options.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_DataAndLongDataSpelling_JoinsThemInOrder()
    {
        CommandLineParseResult result = Parse(["-d", "name=daniel", "--data", "skill=lousy", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        AssertPostData("name=daniel&skill=lousy"u8.ToArray(), result);
        CollectionAssert.AreEqual("name=daniel&skill=lousy"u8.ToArray(), result.Options.PostData!.Value.ToArray());
    }

    // Measured with curl 8.21.0 --libcurl on 2026-09-26: an "&" is added only after a non-empty body,
    // so an empty value in the middle leaves two, and an empty first value leaves none.
    [TestMethod]
    [DataRow(new[] { "a", "", "b" }, new byte[] { 0x61, 0x26, 0x26, 0x62 })]
    [DataRow(new[] { "a", "" }, new byte[] { 0x61, 0x26 })]
    [DataRow(new[] { "", "b" }, new byte[] { 0x62 })]
    [DataRow(new[] { "", "" }, new byte[0])]
    public void Parse_DataWithEmptyValues_JoinsAsCurlDoes(string[] values, byte[] expected)
    {
        string[] arguments = [.. values.SelectMany(value => new[] { "-d", value }), "http://example.com/"];

        CommandLineParseResult result = Parse(arguments);

        Assert.IsTrue(result.IsAccepted);
        AssertPostData(expected, result);
        CollectionAssert.AreEqual(expected, result.Options.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_NoProtocolOptions_LeavesThemNotGiven()
    {
        CommandLineParseResult result = Parse(["tftp://host/file"]);

        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("post data given", false, result.Options.PostData.HasValue);
        Diagnostics.Assert("credentials given", false, result.Options.Credentials is not null);
        Diagnostics.Assert("telnet options", "[]", CommandLineParseDiagnostics.QuoteEach(result.Options.TelnetOptions));
        Diagnostics.Assert("tftp block size", "null", result.Options.TftpBlockSize?.ToString() ?? "null");
        Diagnostics.Assert("tftp no options", false, result.Options.TftpNoOptions);
        Assert.IsNull(result.Options.PostData);
        Assert.IsNull(result.Options.Credentials);
        Assert.IsEmpty(result.Options.TelnetOptions);
        Assert.IsNull(result.Options.TftpBlockSize);
        Assert.IsFalse(result.Options.TftpNoOptions);
    }

    [TestMethod]
    [DataRow("bob:secret", "bob", "secret")]
    [DataRow("bob:se:cret", "bob", "se:cret")]
    [DataRow("bob:", "bob", "")]
    [DataRow(":secret", "", "secret")]
    public void Parse_UserWithColon_SplitsAtTheFirstColon(string value, string expectedUser, string expectedPassword)
    {
        CommandLineParseResult result = Parse(["-u", value, "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        System.Net.NetworkCredential? credentials = CommandLineParseDiagnostics.Peek(result.Options.Credentials);
        Diagnostics.Assert("user name", $"\"{expectedUser}\"", $"\"{credentials?.UserName}\"");
        Diagnostics.Assert("password", $"\"{expectedPassword}\"", $"\"{credentials?.Password}\"");
        Assert.AreEqual(expectedUser, result.Options.Credentials!.UserName);
        Assert.AreEqual(expectedPassword, result.Options.Credentials.Password);
    }

    [TestMethod]
    public void Parse_TelnetOptions_RecordsEveryValueInOrder()
    {
        CommandLineParseResult result = Parse(["-t", "TTYPE=vt100", "-t", "XDISPLOC=host:0", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        AssertTelnetOptions(["TTYPE=vt100", "XDISPLOC=host:0"], result);
        CollectionAssert.AreEqual(new[] { "TTYPE=vt100", "XDISPLOC=host:0" }, result.Options.TelnetOptions.ToArray());
    }

    [TestMethod]
    [DataRow("BOGUS=1")]
    [DataRow("TTYPE")]
    [DataRow("")]
    public void Parse_TelnetOptionCurlRefusesAtTransferTime_IsRecordedNotRefused(string value)
    {
        CommandLineParseResult result = Parse(["--telnet-option", value, "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        AssertTelnetOptions([value], result);
        CollectionAssert.AreEqual(new[] { value }, result.Options.TelnetOptions.ToArray());
    }

    [TestMethod]
    [DataRow("1024", 1024)]
    [DataRow("5", 5)]
    [DataRow("70000", 70000)]
    public void Parse_TftpBlockSize_RecordsTheValueUnclamped(string value, int expected)
    {
        CommandLineParseResult result = Parse(["--tftp-blksize", value, "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("tftp block size", expected, result.Options.TftpBlockSize);
        Assert.AreEqual(expected, result.Options.TftpBlockSize);
    }

    [TestMethod]
    [DataRow("abc", "expected a proper numerical parameter")]
    [DataRow("", "expected a proper numerical parameter")]
    [DataRow("-1", "expected a positive numerical parameter")]
    public void Parse_TftpBlockSizeNotANonNegativeNumber_Refuses(string value, string reason)
    {
        CommandLineParseResult result = Parse(["--tftp-blksize", value]);

        AssertRefused(result, $"curl: option --tftp-blksize: {reason}");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Parse_OnWindows_TftpBlockSizePastTwoToThe31_Refuses()
    {
        CommandLineParseResult result = Parse(["--tftp-blksize", "2147483648"]);

        AssertRefused(result, "curl: option --tftp-blksize: expected a proper numerical parameter");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow("2147483648")]
    [DataRow("9223372036854775807")]
    public void Parse_OnLinuxOrMacOS_TftpBlockSizePastTwoToThe31_RecordsIntMaximum(string value)
    {
        CommandLineParseResult result = Parse(["--tftp-blksize", value, "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("tftp block size", int.MaxValue, result.Options.TftpBlockSize);
        Assert.AreEqual(int.MaxValue, result.Options.TftpBlockSize);
    }

    [TestMethod]
    public void Parse_TftpNoOptions_SetsTheFlag()
    {
        CommandLineParseResult result = Parse(["--tftp-no-options", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("tftp no options", true, result.Options.TftpNoOptions);
        Assert.IsTrue(result.Options.TftpNoOptions);
    }

    [TestMethod]
    [DataRow("-t")]
    [DataRow("-d")]
    [DataRow("--data")]
    [DataRow("-u")]
    [DataRow("--tftp-blksize")]
    public void Parse_ValueOptionAsLastArgument_RefusesAsRequiringParameter(string spelledOption)
    {
        CommandLineParseResult result = Parse(["tftp://host/file", spelledOption]);

        AssertRefused(result, $"curl: option {spelledOption}: requires parameter");
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }

    private void AssertPostData(byte[] expected, CommandLineParseResult result)
    {
        byte[] actual = result.Options!.PostData?.ToArray() ?? [];
        Diagnostics.Bytes("post data", actual);
        Diagnostics.Diff("post data", expected, actual);
    }

    private void AssertTelnetOptions(string[] expected, CommandLineParseResult result) =>
        Diagnostics.Assert(
            "telnet options",
            CommandLineParseDiagnostics.QuoteEach(expected),
            CommandLineParseDiagnostics.QuoteEach(result.Options!.TelnetOptions));

    private void AssertRefused(CommandLineParseResult result, string expectedFirstLine)
    {
        string[] expectedLines = [expectedFirstLine, CommandLineRefusal.TryHelpLine];
        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, CommandLineParseDiagnostics.Peek(result.Refusal)?.ExitCode);
        Diagnostics.Assert(
            "stderr",
            CommandLineParseDiagnostics.QuoteEach(expectedLines),
            CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines ?? []));
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }
}
