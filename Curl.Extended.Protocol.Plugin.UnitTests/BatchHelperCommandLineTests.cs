namespace Curl.Extended.Protocol.Plugin;

/// <summary>Checks the cmd.exe arguments that run a batch helper, so no shell character in a URL starts another command (ADR-1238).</summary>
[TestClass]
public sealed class BatchHelperCommandLineTests
{
    [TestMethod]
    [DataRow("C:/bin/curl-proto-demo.cmd")]
    [DataRow("C:/bin/curl-proto-demo.BAT")]
    [DataRow("C:/bin/curl-proto-demo.Cmd. .")]
    public void IsBatchFile_BatOrCmdIgnoringCaseAndTrailingDotsAndSpaces_IsTrue(string program)
    {
        bool isBatch = BatchHelperCommandLine.IsBatchFile(program);

        Assert.IsTrue(isBatch);
    }

    [TestMethod]
    [DataRow("C:/bin/curl-proto-demo.exe")]
    [DataRow("/usr/bin/curl-proto-demo")]
    [DataRow("C:/bin/curl-proto-demo.cmdx")]
    public void IsBatchFile_OtherProgram_IsFalse(string program)
    {
        bool isBatch = BatchHelperCommandLine.IsBatchFile(program);

        Assert.IsFalse(isBatch);
    }

    [TestMethod]
    public void ArgumentsFor_AmpersandAndShellCharacters_StayInsideTheQuotes()
    {
        string arguments = BatchHelperCommandLine.ArgumentsFor("C:/bin/curl-proto-demo.cmd", "demo://host/a&echo.>pwn1.txt|x<y^z");

        Assert.AreEqual("/e:ON /v:OFF /d /c \"\"C:/bin/curl-proto-demo.cmd\" \"demo://host/a&echo.>pwn1.txt|x<y^z\"\"", arguments);
    }

    [TestMethod]
    public void ArgumentsFor_Percent_IsFollowedByAnEmptyExpansionSoNoVariableExpands()
    {
        string arguments = BatchHelperCommandLine.ArgumentsFor("C:/my%dir/curl-proto-demo.cmd", "demo://host/%PATH%/a%20b");

        Assert.AreEqual("/e:ON /v:OFF /d /c \"\"C:/my%%cd:~,%dir/curl-proto-demo.cmd\" \"demo://host/%%cd:~,%PATH%%cd:~,%/a%%cd:~,%20b\"\"", arguments);
    }

    [TestMethod]
    [DataRow("demo://host/a\"&echo.>pwn2.txt&\"")]
    [DataRow("demo://host/a\r\necho")]
    [DataRow("demo://host/a\nb")]
    [DataRow("demo://host/a\0b")]
    public void ArgumentsFor_UrlWithACharacterCmdCannotCarry_IsRefused(string url)
    {
        IOException failure = Assert.ThrowsExactly<IOException>(() => BatchHelperCommandLine.ArgumentsFor("C:/bin/curl-proto-demo.cmd", url));

        Assert.AreEqual("a batch helper cannot receive a double quote, carriage return, line feed or NUL", failure.Message);
    }

    [TestMethod]
    public void ArgumentsFor_ProgramWithADoubleQuote_IsRefused()
    {
        Assert.ThrowsExactly<IOException>(() => BatchHelperCommandLine.ArgumentsFor("C:/b\"in/curl-proto-demo.cmd", "demo://host/"));
    }
}
