using System.Globalization;
using System.Text;

namespace Curl.Extension.DnsQuery.Wire;

/// <summary>
/// Writes wire octets in master-file presentation format (RFC 1035 section 5.1): a special character after a
/// backslash, an octet outside printable ASCII as <c>\DDD</c>.
/// </summary>
internal static class DnsPresentation
{
    private const string LabelSpecials = ".\\\"();@$";
    private const string CharacterStringSpecials = "\\\"";

    /// <summary>Appends one label of a name, without its dot.</summary>
    /// <param name="builder">Where to write.</param>
    /// <param name="label">The label's octets.</param>
    public static void AppendLabel(StringBuilder builder, ReadOnlySpan<byte> label) => AppendEscaped(builder, label, 0x21, LabelSpecials);

    /// <summary>Gives a character-string's content, without its quotes.</summary>
    /// <param name="octets">The string's octets.</param>
    /// <returns>The presentation text.</returns>
    public static string CharacterString(ReadOnlySpan<byte> octets)
    {
        StringBuilder builder = new();
        AppendEscaped(builder, octets, 0x20, CharacterStringSpecials);
        return builder.ToString();
    }

    private static void AppendEscaped(StringBuilder builder, ReadOnlySpan<byte> octets, int lowestPrintable, string specials)
    {
        foreach (byte octet in octets)
        {
            char character = (char)octet;
            if (octet < lowestPrintable || octet > 0x7E)
            {
                builder.Append('\\').Append(octet.ToString("D3", CultureInfo.InvariantCulture));
            }
            else if (specials.Contains(character, StringComparison.Ordinal))
            {
                builder.Append('\\').Append(character);
            }
            else
            {
                builder.Append(character);
            }
        }
    }
}
