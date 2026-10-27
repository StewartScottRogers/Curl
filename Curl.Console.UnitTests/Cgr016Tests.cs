using System.Text;
using Curl.Authentication;
using Curl.Cli;

namespace Curl.Console;

/// <summary>
/// Pins CGR-016's <c>-b</c> part (BL-3545): curl 8.18.0 on Linux sends <c>-b $'a=\xff'</c> as
/// <c>Cookie: a=</c> and the byte FF, so off Windows the console encodes a <c>-b</c> string's
/// <see cref="InvalidUtf8ByteEscape" /> surrogate as its byte and every other character as UTF-8;
/// on Windows it keeps the ANSI code page.
/// </summary>
[TestClass]
public sealed class Cgr016Tests
{
    [TestMethod]
    public void CookieStringEncodingFor_OffWindowsEscapedByte_GivesTheByte()
    {
        string cookie = "a=" + InvalidUtf8ByteEscape.Decode([0xFF]);

        byte[] bytes = CurlComposition.CookieStringEncodingFor(false).GetBytes(cookie);

        CollectionAssert.AreEqual(new byte[] { 0x61, 0x3D, 0xFF }, bytes);
    }

    [TestMethod]
    public void CookieStringEncodingFor_OffWindowsValidText_GivesItsUtf8Bytes()
    {
        const string Cookie = "c=中é";

        byte[] bytes = CurlComposition.CookieStringEncodingFor(false).GetBytes(Cookie);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(Cookie), bytes);
    }

    [TestMethod]
    public void CookieStringEncodingFor_Windows_IsTheAnsiCodePage()
    {
        Encoding encoding = CurlComposition.CookieStringEncodingFor(true);

        Assert.AreEqual(CredentialEncoding.ForPlatform(true).CodePage, encoding.CodePage);
    }
}
