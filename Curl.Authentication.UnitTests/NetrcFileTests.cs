using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="NetrcFile" /> to curl 8.21.0 (mingw, Git for Windows), measured with
/// Record-CurlExchange.ps1 as <c>curl -s -S --netrc-file &lt;file&gt; http://[user@]127.0.0.1:18503/</c>:
/// each test's expectation is the Basic credentials curl sent, or its exit and message.
/// </summary>
[TestClass]
public sealed class NetrcFileTests
{
    private const string Host = "127.0.0.1";

    private static readonly string LongLineAfterMatch =
        "machine 127.0.0.1 login a password p\n#" + new string('x', 20000) + "\n";

    [TestMethod]
    [DataRow("default login d password dp\nmachine 127.0.0.1 login m password mp\n", "d", "dp", DisplayName = "A default ahead of the machine wins")]
    [DataRow("machine other login o password op\ndefault login d password dp\n", "d", "dp", DisplayName = "A default after a machine for another host")]
    [DataRow("machine 127.0.0.1 login a password pa\nmachine 127.0.0.1 login b password pb\n", "a", "pa", DisplayName = "The first entry for the host wins")]
    [DataRow("machine 127.0.0.1 login q password \"a\\\"b\\\\c\"\n", "q", "a\"b\\c", DisplayName = "A quoted password with escaped quote and backslash")]
    [DataRow("macdef init\nmachine 127.0.0.1 login x password y\n\nmachine 127.0.0.1 login m password mp\n", "m", "mp", DisplayName = "A macdef body is skipped to the blank line")]
    [DataRow("macdef x\nfoo\n  \nmachine 127.0.0.1 login a password p\n\nmachine 127.0.0.1 login b password pb\n", "a", "p", DisplayName = "A whitespace-only line ends a macdef")]
    [DataRow("macdef x machine 127.0.0.1 login a password p\n\nmachine 127.0.0.1 login b password pb\n", "b", "pb", DisplayName = "The rest of the macdef line is skipped")]
    [DataRow("macdef\nmachine 127.0.0.1 login a password p\n\nmachine 127.0.0.1 login b password pb\n", "b", "pb", DisplayName = "A macdef without a name")]
    [DataRow("macdef x\r\nfoo\r\n\r\nmachine 127.0.0.1 login a password p\r\n", "a", "p", DisplayName = "A CRLF blank line ends a macdef")]
    [DataRow("machine 127.0.0.1 login a password p\nmacdef x\nfoo", "a", "p", DisplayName = "A macdef ends the matching entry")]
    [DataRow("machine 127.0.0.1 login a macdef x\nstuff\n\npassword p\n", "a", null, DisplayName = "A macdef ends the entry before its password")]
    [DataRow("default login d macdef x\nfoo\n\nmachine 127.0.0.1 login m password mp\n", "d", null, DisplayName = "A macdef ends a default entry")]
    [DataRow("machine 127.0.0.1 password onlypw\n", null, "onlypw", DisplayName = "An entry with only a password")]
    [DataRow("machine 127.0.0.1 login onlyl\n", "onlyl", null, DisplayName = "An entry with only a login")]
    [DataRow("machine 127.0.0.1 login a\nmachine 127.0.0.1 login b password pb\n", "a", null, DisplayName = "A login-only entry wins over a later full one")]
    [DataRow("machine 127.0.0.1 password p\nmachine 127.0.0.1 login b password pb\n", null, "p", DisplayName = "A password-only entry wins over a later full one")]
    [DataRow("machine 127.0.0.1\nmachine 127.0.0.1 login b password pb\n", "b", "pb", DisplayName = "An empty entry is passed over")]
    [DataRow("default\nmachine 127.0.0.1 login b password pb\n", "b", "pb", DisplayName = "An empty default is passed over")]
    [DataRow("machine 127.0.0.1 login a\nmachine other login o password op\n", "a", null, DisplayName = "A login-only entry ended by another machine")]
    [DataRow("machine 127.0.0.1 login c # password z\npassword cp\n", "c", "cp", DisplayName = "A hash where a keyword is expected comments out the line")]
    [DataRow("machine 127.0.0.1 login c #x password z\npassword cp\n", "c", "cp", DisplayName = "A token starting with a hash comments out the line")]
    [DataRow("machine 127.0.0.1 login c \"#x\" password z\npassword cp\n", "c", "cp", DisplayName = "A quoted token starting with a hash comments out the line")]
    [DataRow("#machine 127.0.0.1 login x password y\nmachine 127.0.0.1 login a password p\n", "a", "p", DisplayName = "A comment line")]
    [DataRow("machine 127.0.0.1 # login x\nlogin a password p\n", "a", "p", DisplayName = "A comment after the host name")]
    [DataRow("machine 127.0.0.1 login #c password p\n", "#c", "p", DisplayName = "A hash where a value is expected is the value")]
    [DataRow("machine 127.0.0.1 login a#b password p#x\n", "a#b", "p#x", DisplayName = "A hash inside a token is kept")]
    [DataRow("machine 127.0.0.1 login \"#a\" password p\n", "#a", "p", DisplayName = "A quoted value starting with a hash")]
    [DataRow("MACHINE 127.0.0.1 LOGIN u PASSWORD p\n", "u", "p", DisplayName = "Keywords in capitals")]
    [DataRow("machine 127.0.0.1 login a foo bar password p\n", "a", "p", DisplayName = "Unknown tokens are ignored")]
    [DataRow("machine 127.0.0.1 login a foo password p\n", "a", "p", DisplayName = "An unknown token takes no value")]
    [DataRow("machine 127.0.0.1 login a account acc password p\n", "a", "p", DisplayName = "account is ignored")]
    [DataRow("machine 127.0.0.1 login \"x\\ny\\tz\\rw\\qv\" password p\n", "x\ny\tz\rwqv", "p", DisplayName = "Quoted escapes")]
    [DataRow("machine \"127.0.0.1\" login \"a b\" password \"c d\"\n", "a b", "c d", DisplayName = "Quoted host, login and password")]
    [DataRow("machine 127.0.0.1 login \"ab\ncd\" password p\n", "ab\ncd", "p", DisplayName = "A quoted token spans lines")]
    [DataRow("machine 127.0.0.1 login \"ab\"cd password p\n", "ab", "p", DisplayName = "A quoted token ends at its closing quote")]
    [DataRow("machine 127.0.0.1 login \"ab\"password p\n", "ab", "p", DisplayName = "A keyword right after a closing quote")]
    [DataRow("machine 127.0.0.1 login a\"b\"c password p\n", "a\"b\"c", "p", DisplayName = "A quote inside an unquoted token is kept")]
    [DataRow("machine 127.0.0.1 login \"\" password p\n", "", "p", DisplayName = "An empty quoted login")]
    [DataRow("machine \"\" login x password y\nmachine 127.0.0.1 login a password p\n", "a", "p", DisplayName = "An empty quoted host name matches nothing")]
    [DataRow("machine 127.0.0.1 login password password p\n", "password", "p", DisplayName = "A keyword as a value")]
    [DataRow("machine 127.0.0.1 login a login b password p\n", "b", "p", DisplayName = "The last login wins")]
    [DataRow("machine 127.0.0.1 password p login a\n", "a", "p", DisplayName = "Password before login")]
    [DataRow("machine 127.0.0.1 password p login a login", "a", "p", DisplayName = "A login keyword without a value keeps the earlier login")]
    [DataRow("machine 127.0.0.1 login a password", "a", null, DisplayName = "A password keyword without a value at the end")]
    [DataRow("machine\t127.0.0.1\r\n\tlogin u\r\n\tpassword p\r\n", "u", "p", DisplayName = "Tabs and CRLF")]
    [DataRow("machine 127.0.0.1 login a password pé\n", "a", "pé", DisplayName = "A non-ASCII password")]
    [DataRow("machine 127.0.0.1 login a password pa\nmachine other login \"abc\n", "a", "pa", DisplayName = "A syntax error after the match is never read")]
    public void Find_WithoutUserName_ReturnsTheEntryCurlPicks(string text, string? expectedLogin, string? expectedPassword)
    {
        NetrcLookupResult result = NetrcFile.Find(text, Host, userName: null);

        AssertFound(result, expectedLogin, expectedPassword);
    }

    [TestMethod]
    [DataRow("machine 127.0.0.1 login a password pa\nmachine 127.0.0.1 login b password pb\n", "b", "b", "pb", DisplayName = "The user name selects the second entry")]
    [DataRow("machine 127.0.0.1 password p\n", "b", null, "p", DisplayName = "An entry without a login serves any user")]
    [DataRow("machine 127.0.0.1 login b\nmachine 127.0.0.1 login b password second\n", "b", "b", "second", DisplayName = "An entry without a password is passed over")]
    [DataRow("machine 127.0.0.1 login a password pa\ndefault login z password zp\n", "z", "z", "zp", DisplayName = "A default whose login is the user")]
    [DataRow("machine 127.0.0.1 login a password pa\ndefault password dp\n", "z", null, "dp", DisplayName = "A default without a login")]
    public void Find_WithUserName_ReturnsTheEntryForThatUser(string text, string userName, string? expectedLogin, string? expectedPassword)
    {
        NetrcLookupResult result = NetrcFile.Find(text, Host, userName);

        AssertFound(result, expectedLogin, expectedPassword);
    }

    [TestMethod]
    [DataRow("", DisplayName = "Empty text")]
    [DataRow("# just a comment\n", DisplayName = "A comment-only file")]
    [DataRow("machine example.com login e password ep\n", DisplayName = "A host that matches nothing")]
    [DataRow("machine 127.0.0.1\n", DisplayName = "A machine line with no login or password")]
    [DataRow("machine 127.0.0.1 login", DisplayName = "A login keyword without a value")]
    [DataRow("machine", DisplayName = "A machine keyword without a host name")]
    [DataRow("machine #127.0.0.1\n127.0.0.1 login a password p\n", DisplayName = "A host name starting with a hash is a host name")]
    [DataRow("machine\f127.0.0.1\flogin\fa\fpassword\fp\n", DisplayName = "A form feed does not separate tokens")]
    [DataRow("login x password y\nmachine 127.0.0.1\n", DisplayName = "A login and password outside any entry")]
    public void Find_WithoutUserName_NoEntry_IsNotFound(string text)
    {
        NetrcLookupResult result = NetrcFile.Find(text, Host, userName: null);

        Assert.AreEqual(NetrcLookupOutcome.NotFound, result.Outcome);
        Assert.IsNull(result.Login);
        Assert.IsNull(result.Password);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    [DataRow("machine 127.0.0.1 login a password pa\n", "z", DisplayName = "A login that is not the user")]
    [DataRow("machine 127.0.0.1 login B password pb\n", "b", DisplayName = "The login is compared case-sensitively")]
    [DataRow("default login d password dp\n", "z", DisplayName = "A default whose login is not the user")]
    [DataRow("machine 127.0.0.1\n", "z", DisplayName = "An empty entry")]
    public void Find_WithUserName_NoEntryForThatUser_IsNotFound(string text, string userName)
    {
        NetrcLookupResult result = NetrcFile.Find(text, Host, userName);

        Assert.AreEqual(NetrcLookupOutcome.NotFound, result.Outcome);
    }

    [TestMethod]
    [DataRow("machine 127.0.0.1 login \"abc\n", DisplayName = "An unterminated quote")]
    [DataRow("machine 127.0.0.1 login \"abc", DisplayName = "An unterminated quote at the end of the text")]
    [DataRow("machine 127.0.0.1 login \"abc\\\" password p\n", DisplayName = "An escaped closing quote")]
    [DataRow("machine 127.0.0.1 login \"abc\\", DisplayName = "A backslash at the end of the text")]
    [DataRow("machine other login \"abc\nmachine 127.0.0.1 login a password pa\n", DisplayName = "An unterminated quote before the match")]
    public void Find_MalformedBeforeAMatch_IsASyntaxError(string text)
    {
        NetrcLookupResult result = NetrcFile.Find(text, Host, userName: null);

        AssertSyntaxError(result);
    }

    [TestMethod]
    public void Find_SyntaxError_CarriesCurlsMessageAndExitCode()
    {
        NetrcLookupResult result = NetrcFile.Find("machine 127.0.0.1 login \"abc\n", Host, userName: null);

        Assert.AreEqual(
            "curl: (26) .netrc error: syntax error",
            $"curl: ({(int)result.ExitCode}) {NetrcLookupResult.SyntaxErrorMessage}");
    }

    [TestMethod]
    [DataRow(4094, false, DisplayName = "4094 bytes")]
    [DataRow(4095, false, DisplayName = "4095 bytes")]
    [DataRow(4096, true, DisplayName = "4096 bytes")]
    [DataRow(5000, true, DisplayName = "5000 bytes")]
    public void Find_UnquotedPasswordLength_IsLimitedTo4095Bytes(int length, bool isSyntaxError)
    {
        string password = new('p', length);

        NetrcLookupResult result = NetrcFile.Find($"machine 127.0.0.1 login a password {password}\n", Host, userName: null);

        AssertFoundOrSyntaxError(result, isSyntaxError, "a", password);
    }

    [TestMethod]
    [DataRow(4095, false, DisplayName = "4095 bytes")]
    [DataRow(4096, true, DisplayName = "4096 bytes")]
    public void Find_QuotedPasswordLength_IsLimitedTo4095Bytes(int length, bool isSyntaxError)
    {
        string password = new('p', length);

        NetrcLookupResult result = NetrcFile.Find($"machine 127.0.0.1 login a password \"{password}\"\n", Host, userName: null);

        AssertFoundOrSyntaxError(result, isSyntaxError, "a", password);
    }

    [TestMethod]
    public void Find_QuotedPassword_LimitCountsTheUnescapedBytes()
    {
        // 4096 characters between the quotes, 4094 once the two escapes are removed.
        string password = "pp" + new string('p', 4092);

        NetrcLookupResult result = NetrcFile.Find($"machine 127.0.0.1 login a password \"\\p\\p{new string('p', 4092)}\"\n", Host, userName: null);

        AssertFound(result, "a", password);
    }

    [TestMethod]
    public void Find_PasswordOf4096Utf8Bytes_IsASyntaxError()
    {
        NetrcLookupResult result = NetrcFile.Find($"machine 127.0.0.1 login a password {new string('é', 2048)}\n", Host, userName: null);

        AssertSyntaxError(result);
    }

    [TestMethod]
    public void Find_CommentLongerThanATokenMayBe_IsSkipped()
    {
        NetrcLookupResult result = NetrcFile.Find($"# {new string('p', 5000)}\nmachine 127.0.0.1 login a password p\n", Host, userName: null);

        AssertFound(result, "a", "p");
    }

    [TestMethod]
    public void Find_MacroLineLongerThanATokenMayBe_IsSkipped()
    {
        NetrcLookupResult result = NetrcFile.Find($"macdef m\n{new string('p', 5000)}\n\nmachine 127.0.0.1 login a password p\n", Host, userName: null);

        AssertFound(result, "a", "p");
    }

    [TestMethod]
    public void Find_HostName_IsMatchedWithoutRegardToCase()
    {
        NetrcLookupResult result = NetrcFile.Find("machine localhost login u password p\n", "LOCALHOST", userName: null);

        AssertFound(result, "u", "p");
    }

    [TestMethod]
    public void Find_LongTokenAfterTheMatch_IsNeverRead()
    {
        NetrcLookupResult result = NetrcFile.Find($"machine 127.0.0.1 login a password p\nmachine x login {new string('p', 5000)}\n", Host, userName: null);

        AssertFound(result, "a", "p");
    }

    [TestMethod]
    [DataRow(16382, false, DisplayName = "16382 bytes")]
    [DataRow(16383, true, DisplayName = "16383 bytes")]
    [DataRow(20000, true, DisplayName = "20000 bytes")]
    public void Find_LineLength_IsLimitedTo16382BytesBeforeItsLineFeed(int length, bool isSyntaxError)
    {
        string text = "#" + new string('x', length - 1) + "\nmachine 127.0.0.1 login a password p\n";

        NetrcLookupResult result = NetrcFile.Find(text, Host, userName: null);

        AssertFoundOrSyntaxError(result, isSyntaxError, "a", "p");
    }

    [TestMethod]
    [DataRow(16382, false, DisplayName = "16382 bytes")]
    [DataRow(16383, true, DisplayName = "16383 bytes")]
    public void Find_LastLineWithoutLineFeed_IsLimitedTo16382Bytes(int length, bool isSyntaxError)
    {
        string text = "machine 127.0.0.1 login a password p\n#" + new string('x', length - 1);

        NetrcLookupResult result = NetrcFile.Find(text, Host, userName: null);

        AssertFoundOrSyntaxError(result, isSyntaxError, "a", "p");
    }

    [TestMethod]
    public void Find_LineOf16383Utf8Bytes_IsASyntaxError()
    {
        string text = "#" + new string('é', 8191) + "\nmachine 127.0.0.1 login a password p\n";

        NetrcLookupResult result = NetrcFile.Find(text, Host, userName: null);

        AssertSyntaxError(result);
    }

    [TestMethod]
    public void Find_LongLineAfterTheMatchingEntryBeforeItEnds_IsASyntaxError()
    {
        // The entry ends only at the end of the text, so the long line is read first.
        NetrcLookupResult result = NetrcFile.Find(LongLineAfterMatch, Host, userName: null);

        AssertSyntaxError(result);
    }

    [TestMethod]
    public void Find_LongLineInsideAMacroDefinition_IsASyntaxError()
    {
        string text = "macdef m\n" + new string('x', 16383) + "\n\nmachine 127.0.0.1 login a password p\n";

        NetrcLookupResult result = NetrcFile.Find(text, Host, userName: null);

        AssertSyntaxError(result);
    }

    [TestMethod]
    public async Task FindAsync_ReadsTheStreamAsUtf8()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("machine 127.0.0.1 login a password pé\n"));

        NetrcLookupResult result = await NetrcFile.FindAsync(stream, Host, userName: null, CancellationToken.None);

        AssertFound(result, "a", "pé");
        Assert.IsTrue(stream.CanRead, "The stream is left open.");
    }

    [TestMethod]
    public async Task FindAsync_KeepsAByteOrderMarkAsText()
    {
        // Measured: glued to "machine", the byte order mark makes it an unknown token.
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("machine 127.0.0.1 login a password p\n")];
        using var stream = new MemoryStream(bytes);

        NetrcLookupResult result = await NetrcFile.FindAsync(stream, Host, userName: null, CancellationToken.None);

        Assert.AreEqual(NetrcLookupOutcome.NotFound, result.Outcome);
    }

    [TestMethod]
    public void Find_NullText_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => NetrcFile.Find(null!, Host, userName: null));
    }

    [TestMethod]
    public void Find_NullHostName_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => NetrcFile.Find(string.Empty, null!, userName: null));
    }

    [TestMethod]
    public async Task FindAsync_NullStream_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => NetrcFile.FindAsync(null!, Host, userName: null, CancellationToken.None));
    }

    private static void AssertFound(NetrcLookupResult result, string? expectedLogin, string? expectedPassword)
    {
        Assert.AreEqual(NetrcLookupOutcome.Found, result.Outcome);
        Assert.AreEqual(expectedLogin, result.Login);
        Assert.AreEqual(expectedPassword, result.Password);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    private static void AssertSyntaxError(NetrcLookupResult result)
    {
        Assert.AreEqual(NetrcLookupOutcome.SyntaxError, result.Outcome);
        Assert.IsNull(result.Login);
        Assert.IsNull(result.Password);
        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
    }

    private static void AssertFoundOrSyntaxError(NetrcLookupResult result, bool isSyntaxError, string expectedLogin, string expectedPassword)
    {
        if (isSyntaxError)
        {
            AssertSyntaxError(result);
        }
        else
        {
            AssertFound(result, expectedLogin, expectedPassword);
        }
    }
}
