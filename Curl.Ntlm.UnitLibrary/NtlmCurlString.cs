using System.Text;

namespace Curl.Ntlm;

/// <summary>
/// Strings as curl 8.21.0 turns them into NTLM bytes: its C strings are the UTF-8 bytes,
/// and "Unicode" is each byte widened to a 16-bit little-endian unit
/// (<c>unicodecpy</c> in <c>lib/vauth/ntlm.c</c>, <c>ascii_to_unicode_le</c> and
/// <c>ascii_uppercase_to_unicode_le</c> in <c>lib/curl_ntlm_core.c</c>). That is UTF-16LE
/// for ASCII and differs from it for any other character, as curl's does.
/// </summary>
internal static class NtlmCurlString
{
    /// <summary>Each UTF-8 byte of <paramref name="text" /> widened to a 16-bit little-endian unit.</summary>
    public static byte[] Widen(string text) => WidenBytes(Encoding.UTF8.GetBytes(text));

    /// <summary>
    /// As <see cref="Widen" />, with ASCII <c>a</c> to <c>z</c> uppercased first, as curl's
    /// <c>Curl_raw_toupper</c> does; no other byte changes.
    /// </summary>
    public static byte[] WidenUppercase(string text) => WidenBytes(UppercaseAscii(Encoding.UTF8.GetBytes(text)));

    /// <summary>ASCII <c>a</c> to <c>z</c> in <paramref name="bytes" /> uppercased in place, which it returns.</summary>
    public static byte[] UppercaseAscii(byte[] bytes)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] is >= (byte)'a' and <= (byte)'z')
            {
                bytes[index] -= 'a' - 'A';
            }
        }

        return bytes;
    }

    /// <summary>Each of <paramref name="bytes" /> as the low byte of a 16-bit little-endian unit.</summary>
    public static byte[] WidenBytes(ReadOnlySpan<byte> bytes)
    {
        byte[] widened = new byte[bytes.Length * 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            widened[2 * index] = bytes[index];
        }

        return widened;
    }
}
