using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>-b</c>/<c>--cookie</c>, <c>-c</c>/<c>--cookie-jar</c> and
/// <c>-j</c>/<c>--junk-session-cookies</c>, measured against the local curl 8.21.0 (mingw, Schannel)
/// on 2026-09-26 with <c>curl &lt;arguments&gt; http://127.0.0.1:1/</c>, reading standard error
/// and the exit code:
/// <list type="bullet">
/// <item><c>-b ''</c>, <c>--cookie ''</c>, <c>-b nonexist.txt</c>, <c>-b a=1</c>, <c>-b -x</c> and
/// <c>-c -x</c> are accepted with no warning (exit 7, the connection refused).</item>
/// <item><c>-c ''</c> and <c>--cookie-jar ''</c> exit 2 with <c>curl: option -c: blank argument where
/// content is expected</c> and the try-help line.</item>
/// <item><c>-b</c> or <c>-c</c> as the last argument exits 2 with <c>curl: option -b: requires
/// parameter</c> and the try-help line.</item>
/// <item><c>--no-cookie</c>, <c>--no-cookie=x</c>, <c>--no-cookie-jar</c> and <c>--no-cookie-jar=x</c>
/// exit 2 with <c>curl: option &lt;as typed&gt;: the given option cannot be reversed with a --no-
/// prefix</c> and the try-help line.</item>
/// <item><c>--no-junk-session-cookies</c> and <c>--no-junk-session-cookies=x</c> are accepted.</item>
/// </list>
/// </summary>
[TestClass]
public sealed class CommandLineCookieOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Parse_NoCookieOptions_HasNoCookiesJarOrJunking()
    {
        CommandLineParseResult result = Parse([Url]);

        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        diagnostics.Assert("cookie count", 0, result.Options.Cookies.Count);
        diagnostics.Assert("cookie jar", null, result.Options.CookieJar);
        diagnostics.Assert("junk session cookies", false, result.Options.JunkSessionCookies);
        Assert.IsEmpty(result.Options.Cookies);
        Assert.IsNull(result.Options.CookieJar);
        Assert.IsFalse(result.Options.JunkSessionCookies);
    }

    [TestMethod]
    [DataRow("-b")]
    [DataRow("--cookie")]
    public void Parse_CookieWithEquals_IsCookieString(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, "a=1; b=2", Url]);

        TestDiagnostics.For(TestContext).Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CommandLineCookie cookie = result.Options.Cookies.Single();
        AssertCookie("a=1; b=2", true, cookie);
        Assert.AreEqual("a=1; b=2", cookie.Value);
        Assert.IsTrue(cookie.IsCookieString);
    }

    [TestMethod]
    [DataRow("-b")]
    [DataRow("--cookie")]
    public void Parse_CookieWithoutEquals_IsFileName(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, "jar.txt", Url]);

        TestDiagnostics.For(TestContext).Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CommandLineCookie cookie = result.Options.Cookies.Single();
        AssertCookie("jar.txt", false, cookie);
        Assert.AreEqual("jar.txt", cookie.Value);
        Assert.IsFalse(cookie.IsCookieString);
    }

    [TestMethod]
    [DataRow("-b")]
    [DataRow("--cookie")]
    public void Parse_CookieEmptyValue_IsAcceptedAsFileName(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, "", Url]);

        TestDiagnostics.For(TestContext).Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CommandLineCookie cookie = result.Options.Cookies.Single();
        AssertCookie("", false, cookie);
        TestDiagnostics.For(TestContext).Assert("warning line count", 0, result.WarningLines.Count);
        Assert.AreEqual("", cookie.Value);
        Assert.IsFalse(cookie.IsCookieString);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_SeveralCookies_KeepsThemInCommandLineOrder()
    {
        CommandLineParseResult result = Parse(["-b", "jar.txt", "--cookie", "a=1", "-b", "other.txt", Url]);

        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        diagnostics.Assert(
            "cookies",
            CommandLineParseDiagnostics.QuoteEach(["jar.txt", "a=1", "other.txt"]),
            CommandLineParseDiagnostics.QuoteEach(result.Options.Cookies.Select(cookie => cookie.Value)));
        CollectionAssert.AreEqual(
            new[] { new CommandLineCookie("jar.txt"), new CommandLineCookie("a=1"), new CommandLineCookie("other.txt") },
            result.Options.Cookies.ToArray());
    }

    [TestMethod]
    public void With_NewValue_ReclassifiesCookie()
    {
        CommandLineCookie file = new("jar.txt");
        TestDiagnostics.For(TestContext).Arrange("cookie", "\"jar.txt\"");

        CommandLineCookie replaced = file with { Value = "a=1" };

        TestDiagnostics.For(TestContext).Act("replaced", $"\"{replaced.Value}\", cookie string {replaced.IsCookieString}");
        AssertCookie("a=1", true, replaced);
        TestDiagnostics.For(TestContext).Assert("original is cookie string", false, file.IsCookieString);
        Assert.AreEqual("a=1", replaced.Value);
        Assert.IsTrue(replaced.IsCookieString);
        Assert.IsFalse(file.IsCookieString);
    }

    [TestMethod]
    [DataRow("-b")]
    [DataRow("-c")]
    public void Parse_CookieFileNameLookingLikeFlag_IsAcceptedWithoutWarning(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, "-x", Url]);

        TestDiagnostics.For(TestContext).Assert("accepted", true, result.IsAccepted);
        TestDiagnostics.For(TestContext).Assert("warning line count", 0, result.WarningLines.Count);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("-c")]
    [DataRow("--cookie-jar")]
    public void Parse_CookieJar_RecordsFile(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, "jar.txt", Url]);

        TestDiagnostics.For(TestContext).Assert("cookie jar", "jar.txt", result.Options?.CookieJar);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("jar.txt", result.Options.CookieJar);
    }

    [TestMethod]
    public void Parse_CookieJarTwice_LastWins()
    {
        CommandLineParseResult result = Parse(["-c", "first.txt", "-c", "-", Url]);

        TestDiagnostics.For(TestContext).Assert("cookie jar", "-", result.Options?.CookieJar);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("-", result.Options.CookieJar);
    }

    [TestMethod]
    [DataRow("-c")]
    [DataRow("--cookie-jar")]
    public void Parse_CookieJarEmptyValue_RefusesAsBlank(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, "", Url]);

        AssertRefused(result, $"curl: option {spelledOption}: blank argument where content is expected");
    }

    [TestMethod]
    [DataRow("-b")]
    [DataRow("--cookie")]
    [DataRow("-c")]
    [DataRow("--cookie-jar")]
    public void Parse_CookieOptionAsLastArgument_RefusesAsRequiringParameter(string spelledOption)
    {
        CommandLineParseResult result = Parse([Url, spelledOption]);

        AssertRefused(result, $"curl: option {spelledOption}: requires parameter");
    }

    [TestMethod]
    [DataRow("--no-cookie")]
    [DataRow("--no-cookie=x")]
    public void Parse_NoCookie_IsRefusedAsNotReversible(string argument)
    {
        CommandLineParseResult result = Parse([argument, Url]);

        AssertRefused(result, $"curl: option {argument}: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    [DataRow("--no-cookie-jar")]
    [DataRow("--no-cookie-jar=x")]
    public void Parse_NoCookieJar_IsRefusedAsNotReversible(string argument)
    {
        CommandLineParseResult result = Parse([argument, Url]);

        AssertRefused(result, $"curl: option {argument}: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    [DataRow("-j")]
    [DataRow("--junk-session-cookies")]
    public void Parse_JunkSessionCookies_JunksSessionCookies(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, Url]);

        TestDiagnostics.For(TestContext).Assert("junk session cookies", true, result.Options?.JunkSessionCookies);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.JunkSessionCookies);
    }

    [TestMethod]
    [DataRow("--no-junk-session-cookies")]
    [DataRow("--no-junk-session-cookies=x")]
    public void Parse_JunkSessionCookiesThenNoJunkSessionCookies_KeepsSessionCookies(string negation)
    {
        CommandLineParseResult result = Parse(["-j", negation, Url]);

        TestDiagnostics.For(TestContext).Assert("junk session cookies", false, result.Options?.JunkSessionCookies);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.JunkSessionCookies);
    }

    [TestMethod]
    public void Parse_NoJunkSessionCookiesThenJunkSessionCookies_JunksSessionCookies()
    {
        CommandLineParseResult result = Parse(["--no-junk-session-cookies", "-j", Url]);

        TestDiagnostics.For(TestContext).Assert("junk session cookies", true, result.Options?.JunkSessionCookies);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.JunkSessionCookies);
    }

    private CommandLineParseResult Parse(string[] arguments)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        diagnostics.ActParse(result);
        return result;
    }

    private void AssertCookie(string value, bool isCookieString, CommandLineCookie cookie)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("cookie value", "\"" + value + "\"", "\"" + cookie.Value + "\"");
        diagnostics.Assert("cookie is cookie string", isCookieString, cookie.IsCookieString);
    }

    private void AssertRefused(CommandLineParseResult result, string expectedFirstLine)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("accepted", false, result.IsAccepted);
        diagnostics.Assert("exit code", CurlExitCode.FailedInit, result.Refusal?.ExitCode);
        diagnostics.Assert("first stderr line", expectedFirstLine, result.Refusal?.StandardErrorLines.FirstOrDefault());
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }
}
