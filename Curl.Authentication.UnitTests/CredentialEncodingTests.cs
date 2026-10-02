using System.Text;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="CredentialEncoding" />: the system ANSI code page on Windows, which is how
/// the mingw reference curl receives its arguments, and UTF-8 elsewhere (ADR-0022). Linux and
/// macOS have no system ANSI code page, so Windows behaviour asked for there is Windows-1252.
/// </summary>
[TestClass]
public sealed class CredentialEncodingTests
{
    [TestMethod]
    public void ForPlatform_Windows_IsTheSystemAnsiCodePage()
    {
        // Windows answers code page 0 with its system ANSI code page; Linux and macOS have
        // none and answer null, so there Windows behaviour falls back to Windows-1252. Windows
        // fails one of several concurrent first reads of code page 0, so the read is retried
        // here as in the code under test (BL-1200).
        int expectedCodePage = OperatingSystem.IsWindows()
            ? (CodePagesEncodingProvider.Instance.GetEncoding(0) ?? CodePagesEncodingProvider.Instance.GetEncoding(0))!.CodePage
            : 1252;

        Encoding encoding = CredentialEncoding.ForPlatform(isWindows: true);

        Assert.AreEqual(expectedCodePage, encoding.CodePage);
    }

    [TestMethod]
    public void ForPlatformGivenSystemAnsiCodePage_Windows_IsThatCodePage()
    {
        Encoding shiftJis = CodePagesEncodingProvider.Instance.GetEncoding(932)!;

        Encoding encoding = CredentialEncoding.ForPlatformGivenSystemAnsiCodePage(isWindows: true, systemAnsiCodePage: shiftJis);

        Assert.AreEqual(932, encoding.CodePage);
    }

    [TestMethod]
    public void ForPlatformGivenSystemAnsiCodePage_WindowsOnAHostWithoutOne_IsWindows1252()
    {
        Encoding encoding = CredentialEncoding.ForPlatformGivenSystemAnsiCodePage(isWindows: true, systemAnsiCodePage: null);

        Assert.AreEqual(1252, encoding.CodePage);
    }

    [TestMethod]
    public void ReadSystemAnsiCodePage_FirstReadAnswersACodePage_ReadsOnce()
    {
        Encoding shiftJis = CodePagesEncodingProvider.Instance.GetEncoding(932)!;
        int reads = 0;

        Encoding? encoding = CredentialEncoding.ReadSystemAnsiCodePage(() => { reads++; return shiftJis; });

        Assert.AreEqual(932, encoding!.CodePage);
        Assert.AreEqual(1, reads);
    }

    [TestMethod]
    public void ReadSystemAnsiCodePage_FirstReadFails_ReadsAgain()
    {
        Encoding shiftJis = CodePagesEncodingProvider.Instance.GetEncoding(932)!;
        int reads = 0;

        Encoding? encoding = CredentialEncoding.ReadSystemAnsiCodePage(() => ++reads == 1 ? null : shiftJis);

        Assert.AreEqual(932, encoding!.CodePage);
        Assert.AreEqual(2, reads);
    }

    [TestMethod]
    public void ReadSystemAnsiCodePage_HostHasNone_IsNullAfterTwoReads()
    {
        int reads = 0;

        Encoding? encoding = CredentialEncoding.ReadSystemAnsiCodePage(() => { reads++; return null; });

        Assert.IsNull(encoding);
        Assert.AreEqual(2, reads);
    }

    [TestMethod]
    public void ForPlatform_NotWindows_IsUtf8()
    {
        Encoding encoding = CredentialEncoding.ForPlatform(isWindows: false);

        Assert.AreEqual(Encoding.UTF8.CodePage, encoding.CodePage);
    }
}
