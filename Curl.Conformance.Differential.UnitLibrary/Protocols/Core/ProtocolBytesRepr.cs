using System.Globalization;
using System.Text;

namespace Curl.Conformance.Differential.Protocols.Core;

/// <summary>Formats bytes the way Python's <c>repr(bytes)</c> does, which proto2_servers.py logs for Gopher and Telnet.</summary>
public static class ProtocolBytesRepr
{
    /// <summary>Returns <c>b'...'</c> with Python's quote choice and escapes: backslash, <c>\t</c>, <c>\n</c>, <c>\r</c>, and <c>\xNN</c> outside printable ASCII.</summary>
    public static string Format(ReadOnlySpan<byte> bytes)
    {
        var quote = bytes.Contains((byte)'\'') && !bytes.Contains((byte)'"') ? '"' : '\'';
        var text = new StringBuilder("b").Append(quote);
        foreach (var value in bytes)
        {
            text.Append(Escape(value, quote));
        }

        return text.Append(quote).ToString();
    }

    private static string Escape(byte value, char quote)
    {
        if (value == quote)
        {
            return "\\" + quote;
        }

        return NamedEscape(value) ?? (value is < 0x20 or >= 0x7f ? "\\x" + value.ToString("x2", CultureInfo.InvariantCulture) : ((char)value).ToString());
    }

    private static string? NamedEscape(byte value) => value switch
    {
        (byte)'\\' => "\\\\",
        (byte)'\t' => "\\t",
        (byte)'\n' => "\\n",
        (byte)'\r' => "\\r",
        _ => null,
    };
}
