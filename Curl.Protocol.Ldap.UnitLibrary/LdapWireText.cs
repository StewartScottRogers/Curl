using System.Text;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Turns the text an LDAP URL names - its DN, attributes and filter, held as byte strings,
/// one <see cref="char" /> per byte - into the bytes each reference build puts on the wire
/// (ADR-0166, measured by BL-587).
/// </summary>
/// <remarks>
/// The OpenLDAP build sends the URL's bytes as they are. The Windows build hands them to
/// WinLDAP's ANSI entry points, which read them in the system's ANSI code page and send
/// UTF-8, so <c>%C3%A9</c> goes out as <c>c3 83 c2 a9</c> and <c>%80</c> as <c>e2 82 ac</c>.
/// The code page here is Windows-1252, the ANSI code page of the Windows the reference
/// build was measured on.
/// </remarks>
internal static class LdapWireText
{
    /// <summary>Windows-1252's characters for the bytes 0x80 to 0x9F; every other byte is its Latin-1 character.</summary>
    private const string Windows1252High =
        "€\u0081‚ƒ„…†‡ˆ‰Š‹Œ\u008DŽ\u008F" +
        "\u0090‘’“”•–—˜™š›œ\u009DžŸ";

    /// <summary>Gets the bytes <paramref name="dialect" />'s build sends for <paramref name="byteString" />.</summary>
    /// <param name="dialect">The build to answer as.</param>
    /// <param name="byteString">The text, one <see cref="char" /> (0 to 255) per byte.</param>
    /// <returns>The bytes on the wire.</returns>
    public static byte[] Encode(LdapDialect dialect, string byteString) =>
        dialect == LdapDialect.WinLdap
            ? Encoding.UTF8.GetBytes(string.Concat(byteString.Select(FromWindows1252)))
            : Encoding.Latin1.GetBytes(byteString);

    /// <summary>Gets the byte string of <paramref name="text" />'s UTF-8 bytes.</summary>
    /// <param name="text">Text as a URL holds it.</param>
    /// <returns>One <see cref="char" /> per UTF-8 byte.</returns>
    public static string ToByteString(string text) => Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(text));

    private static char FromWindows1252(char octet) =>
        octet is >= '\u0080' and <= '\u009F' ? Windows1252High[octet - 0x80] : octet;
}
