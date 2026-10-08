using System.Text;
using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="CredentialEncoding" />: the system ANSI code page on Windows, which is how
/// the mingw reference curl receives its arguments, and UTF-8 elsewhere (ADR-0022). Linux and
/// macOS have no system ANSI code page, so Windows behaviour asked for there is Windows-1252.
/// </summary>
[TestClass]
public sealed class CredentialEncodingTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ForPlatform_Windows_IsTheSystemAnsiCodePage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("isWindows", true);

        // Windows answers code page 0 with its system ANSI code page; Linux and macOS have
        // none and answer null, so there Windows behaviour falls back to Windows-1252. Windows
        // fails one of several concurrent first reads of code page 0, so the read is retried
        // here as in the code under test (BL-1200).
        int expectedCodePage = OperatingSystem.IsWindows()
            ? (CodePagesEncodingProvider.Instance.GetEncoding(0) ?? CodePagesEncodingProvider.Instance.GetEncoding(0))!.CodePage
            : 1252;

        Encoding encoding = CredentialEncoding.ForPlatform(isWindows: true);

        diagnostics.Act("code page", encoding.CodePage);
        diagnostics.Assert("code page", expectedCodePage, encoding.CodePage);
        Assert.AreEqual(expectedCodePage, encoding.CodePage);
    }

    [TestMethod]
    public void ForPlatformGivenSystemAnsiCodePage_Windows_IsThatCodePage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Encoding shiftJis = CodePagesEncodingProvider.Instance.GetEncoding(932)!;
        diagnostics.Arrange("system ANSI code page", shiftJis.CodePage);

        Encoding encoding = CredentialEncoding.ForPlatformGivenSystemAnsiCodePage(isWindows: true, systemAnsiCodePage: shiftJis);

        diagnostics.Act("code page", encoding.CodePage);
        diagnostics.Assert("code page", 932, encoding.CodePage);
        Assert.AreEqual(932, encoding.CodePage);
    }

    [TestMethod]
    public void ForPlatformGivenSystemAnsiCodePage_WindowsOnAHostWithoutOne_IsWindows1252()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("system ANSI code page", null);

        Encoding encoding = CredentialEncoding.ForPlatformGivenSystemAnsiCodePage(isWindows: true, systemAnsiCodePage: null);

        diagnostics.Act("code page", encoding.CodePage);
        diagnostics.Assert("code page", 1252, encoding.CodePage);
        Assert.AreEqual(1252, encoding.CodePage);
    }

    [TestMethod]
    public void ReadSystemAnsiCodePage_FirstReadAnswersACodePage_ReadsOnce()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Encoding shiftJis = CodePagesEncodingProvider.Instance.GetEncoding(932)!;
        int reads = 0;
        diagnostics.Arrange("first read answers", shiftJis.CodePage);

        Encoding? encoding = CredentialEncoding.ReadSystemAnsiCodePage(() => { reads++; return shiftJis; });

        diagnostics.Act("code page", encoding?.CodePage);
        diagnostics.Act("reads", reads);
        diagnostics.Assert("code page", 932, encoding!.CodePage);
        diagnostics.Assert("reads", 1, reads);
        Assert.AreEqual(932, encoding!.CodePage);
        Assert.AreEqual(1, reads);
    }

    [TestMethod]
    public void ReadSystemAnsiCodePage_FirstReadFails_ReadsAgain()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Encoding shiftJis = CodePagesEncodingProvider.Instance.GetEncoding(932)!;
        int reads = 0;
        diagnostics.Arrange("first read answers", null);
        diagnostics.Arrange("second read answers", shiftJis.CodePage);

        Encoding? encoding = CredentialEncoding.ReadSystemAnsiCodePage(() => ++reads == 1 ? null : shiftJis);

        diagnostics.Act("code page", encoding?.CodePage);
        diagnostics.Act("reads", reads);
        diagnostics.Assert("code page", 932, encoding!.CodePage);
        diagnostics.Assert("reads", 2, reads);
        Assert.AreEqual(932, encoding!.CodePage);
        Assert.AreEqual(2, reads);
    }

    [TestMethod]
    public void ReadSystemAnsiCodePage_HostHasNone_IsNullAfterTwoReads()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        int reads = 0;
        diagnostics.Arrange("every read answers", null);

        Encoding? encoding = CredentialEncoding.ReadSystemAnsiCodePage(() => { reads++; return null; });

        diagnostics.Act("encoding", encoding);
        diagnostics.Act("reads", reads);
        diagnostics.Assert("encoding", null, encoding);
        diagnostics.Assert("reads", 2, reads);
        Assert.IsNull(encoding);
        Assert.AreEqual(2, reads);
    }

    [TestMethod]
    public void ForPlatform_NotWindows_IsUtf8()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("isWindows", false);

        Encoding encoding = CredentialEncoding.ForPlatform(isWindows: false);

        diagnostics.Act("code page", encoding.CodePage);
        diagnostics.Assert("code page", Encoding.UTF8.CodePage, encoding.CodePage);
        Assert.AreEqual(Encoding.UTF8.CodePage, encoding.CodePage);
    }
}
