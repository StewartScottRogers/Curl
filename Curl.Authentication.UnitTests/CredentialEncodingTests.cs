using System.Text;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="CredentialEncoding" />: the system ANSI code page on Windows, which is how
/// the mingw reference curl receives its arguments, and UTF-8 elsewhere (ADR-0022).
/// </summary>
[TestClass]
public sealed class CredentialEncodingTests
{
    [TestMethod]
    public void ForPlatform_Windows_IsTheSystemAnsiCodePage()
    {
        Encoding encoding = CredentialEncoding.ForPlatform(isWindows: true);

        Assert.AreEqual(CodePagesEncodingProvider.Instance.GetEncoding(0)!.CodePage, encoding.CodePage);
    }

    [TestMethod]
    public void ForPlatform_NotWindows_IsUtf8()
    {
        Encoding encoding = CredentialEncoding.ForPlatform(isWindows: false);

        Assert.AreEqual(Encoding.UTF8.CodePage, encoding.CodePage);
    }
}
