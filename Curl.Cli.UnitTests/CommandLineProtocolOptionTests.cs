using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records the Phase 4 protocol options under their ADR-0006 names:
/// <c>-d</c>/<c>--data</c> as UTF-8 <see cref="CommandLineOptions.PostData"/>,
/// <c>-u</c>/<c>--user</c> split at the first colon, every <c>-t</c>/<c>--telnet-option</c>
/// verbatim and in order, <c>--tftp-blksize</c> unclamped, and <c>--tftp-no-options</c>.
/// Refusal lines were measured against the local curl 8.21.0 on 2026-09-26.
/// </summary>
[TestClass]
public sealed class CommandLineProtocolOptionTests
{
    [TestMethod]
    [DataRow("-d")]
    [DataRow("--data")]
    public void Parse_Data_RecordsUtf8Bytes(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, "75", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new byte[] { 0x37, 0x35 }, result.Options.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_DataWithNonAsciiText_RecordsUtf8Bytes()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-d", "é", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new byte[] { 0xC3, 0xA9 }, result.Options.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_EmptyData_RecordsEmptyData()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--data=", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.PostData.HasValue);
        Assert.AreEqual(0, result.Options.PostData.Value.Length);
    }

    [TestMethod]
    public void Parse_DataGivenTwice_JoinsThemWithAnAmpersand()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-d", "a", "-d", "b", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new byte[] { 0x61, 0x26, 0x62 }, result.Options.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_DataAndLongDataSpelling_JoinsThemInOrder()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-d", "name=daniel", "--data", "skill=lousy", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
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

        CommandLineParseResult result = CommandLineParser.Parse(arguments);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(expected, result.Options.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_NoProtocolOptions_LeavesThemNotGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["tftp://host/file"]);

        Assert.IsTrue(result.IsAccepted);
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
        CommandLineParseResult result = CommandLineParser.Parse(["-u", value, "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expectedUser, result.Options.Credentials!.UserName);
        Assert.AreEqual(expectedPassword, result.Options.Credentials.Password);
    }

    [TestMethod]
    public void Parse_UserWithoutColon_RecordsUserWithEmptyPassword()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--user", "bob", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("bob", result.Options.Credentials!.UserName);
        Assert.AreEqual(string.Empty, result.Options.Credentials.Password);
    }

    [TestMethod]
    public void Parse_TelnetOptions_RecordsEveryValueInOrder()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-t", "TTYPE=vt100", "-t", "XDISPLOC=host:0", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "TTYPE=vt100", "XDISPLOC=host:0" }, result.Options.TelnetOptions.ToArray());
    }

    [TestMethod]
    [DataRow("BOGUS=1")]
    [DataRow("TTYPE")]
    [DataRow("")]
    public void Parse_TelnetOptionCurlRefusesAtTransferTime_IsRecordedNotRefused(string value)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--telnet-option", value, "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { value }, result.Options.TelnetOptions.ToArray());
    }

    [TestMethod]
    [DataRow("1024", 1024)]
    [DataRow("5", 5)]
    [DataRow("70000", 70000)]
    public void Parse_TftpBlockSize_RecordsTheValueUnclamped(string value, int expected)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--tftp-blksize", value, "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.TftpBlockSize);
    }

    [TestMethod]
    [DataRow("abc", "expected a proper numerical parameter")]
    [DataRow("", "expected a proper numerical parameter")]
    [DataRow("-1", "expected a positive numerical parameter")]
    public void Parse_TftpBlockSizeNotANonNegativeNumber_Refuses(string value, string reason)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--tftp-blksize", value]);

        AssertRefused(result, $"curl: option --tftp-blksize: {reason}");
    }

    [TestMethod]
    public void Parse_TftpNoOptions_SetsTheFlag()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--tftp-no-options", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
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
        CommandLineParseResult result = CommandLineParser.Parse(["tftp://host/file", spelledOption]);

        AssertRefused(result, $"curl: option {spelledOption}: requires parameter");
    }

    private static void AssertRefused(CommandLineParseResult result, string expectedFirstLine)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }
}
