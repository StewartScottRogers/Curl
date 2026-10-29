using System.Globalization;
using System.Text;

namespace Curl.Authentication;

/// <summary>
/// Reads a <c>-u</c> user name as curl's Windows SSPI build does (CGR-209): the name reaches
/// SSPI as ANSI bytes, which the Type-3 message then reads through the OEM code page, so
/// <c>Ü</c> (ANSI 0xDC) becomes <c>▄</c> (OEM 437).
/// </summary>
internal static class NtlmUserNameCodePages
{
    static NtlmUserNameCodePages() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>
    /// Gets <paramref name="userName" /> encoded with this machine's ANSI code page and read
    /// back through its OEM code page. Where both are UTF-8 (Linux and macOS) that is the name itself.
    /// </summary>
    public static string ReadAsSspiDoes(string userName)
    {
        TextInfo text = CultureInfo.CurrentCulture.TextInfo;
        return ReadAsSspiDoes(userName, Encoding.GetEncoding(text.ANSICodePage), Encoding.GetEncoding(text.OEMCodePage));
    }

    /// <summary>Gets <paramref name="userName" /> encoded with <paramref name="ansi" /> and decoded with <paramref name="oem" />.</summary>
    public static string ReadAsSspiDoes(string userName, Encoding ansi, Encoding oem) =>
        oem.GetString(ansi.GetBytes(userName));
}
