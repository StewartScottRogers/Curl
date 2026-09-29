using System.Text;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Formats one SearchResultEntry as each reference build writes it to the output (ADR-0166,
/// measured by BL-588 against curl 8.21.0 with WinLDAP and curl 8.18.0 with OpenLDAP 2.6.10).
/// </summary>
/// <remarks>
/// <para>
/// Both builds write a <c>DN:</c> line, then for each attribute one tab-indented line per
/// value and a blank line after the attribute. A value is written in base64 after <c>::</c>
/// when the attribute name is longer than <c>;binary</c> and ends with it in any case, or when
/// the value is not printable: it starts or ends with a space or a tab, or holds a byte that
/// is neither printable ASCII nor one of tab, line feed, vertical tab, form feed and carriage
/// return. Otherwise the value is written as it is.
/// </para>
/// <para>
/// WinLDAP: <c>DN: &lt;dn&gt;</c>, each value <c>\t&lt;name&gt;: &lt;value&gt;</c> or
/// <c>\t&lt;name&gt;:: &lt;base64&gt;</c> even when empty, an attribute with no values as the
/// blank line alone, and the DN and names in the ANSI code page
/// (<see cref="LdapWireText.ToWindowsAnsi" />); an attribute whose name does not survive that
/// is written with no values, and an entry whose attributes cannot be read as its DN line alone.
/// </para>
/// <para>
/// OpenLDAP: the text as sent, <c>DN:</c> and each value's separator followed by a space and
/// the text only when there is text, an attribute with no values as <c>\t&lt;name&gt;:</c> with
/// no blank line after it, and one more blank line after the entry.
/// </para>
/// </remarks>
internal static class LdapEntryFormatter
{
    private const string BinarySuffix = ";binary";

    /// <summary>Gets the bytes <paramref name="dialect" />'s build writes for <paramref name="entry" />.</summary>
    /// <param name="dialect">The build to answer as.</param>
    /// <param name="entry">
    /// The entry; for <see cref="LdapDialect.OpenLdap" />, one whose attributes could be read,
    /// since that build fails on any other before writing it.
    /// </param>
    /// <returns>The bytes to write.</returns>
    public static byte[] Format(LdapDialect dialect, LdapSearchEntry entry)
    {
        var text = new MemoryStream();
        IReadOnlyList<LdapEntryAttribute> attributes = entry.Attributes ?? [];
        if (dialect == LdapDialect.WinLdap)
        {
            WriteWinLdap(text, entry.Dn, attributes);
        }
        else
        {
            WriteOpenLdap(text, entry.Dn, attributes);
        }

        return text.ToArray();
    }

    private static void WriteWinLdap(MemoryStream text, byte[] dn, IReadOnlyList<LdapEntryAttribute> attributes)
    {
        Write(text, "DN: ");
        text.Write(LdapWireText.ToWindowsAnsi(dn));
        Write(text, "\n");
        foreach (LdapEntryAttribute attribute in attributes)
        {
            byte[] name = LdapWireText.ToWindowsAnsi(attribute.Name);
            IReadOnlyList<byte[]> values = LdapWireText.SurvivesWindowsAnsi(attribute.Name) ? attribute.Values : [];
            foreach (byte[] value in values)
            {
                Write(text, "\t");
                text.Write(name);
                bool binary = IsBinary(name, value);
                Write(text, binary ? ":: " : ": ");
                text.Write(binary ? Base64(value) : value);
                Write(text, "\n");
            }

            Write(text, "\n");
        }
    }

    private static void WriteOpenLdap(MemoryStream text, byte[] dn, IReadOnlyList<LdapEntryAttribute> attributes)
    {
        Write(text, "DN:");
        WriteSpacedText(text, dn);
        Write(text, "\n");
        foreach (LdapEntryAttribute attribute in attributes)
        {
            if (attribute.Values.Count == 0)
            {
                WriteNameAndColon(text, attribute.Name);
                Write(text, "\n");
                continue;
            }

            foreach (byte[] value in attribute.Values)
            {
                WriteNameAndColon(text, attribute.Name);
                bool binary = IsBinary(attribute.Name, value);
                Write(text, binary ? ":" : string.Empty);
                WriteSpacedText(text, binary ? Base64(value) : value);
                Write(text, "\n");
            }

            Write(text, "\n");
        }

        Write(text, "\n");
    }

    /// <summary>Writes a space and <paramref name="value" /> when there is any; nothing otherwise, as <c>libldap</c>'s build does.</summary>
    private static void WriteSpacedText(MemoryStream text, byte[] value)
    {
        if (value.Length > 0)
        {
            Write(text, " ");
            text.Write(value);
        }
    }

    private static void WriteNameAndColon(MemoryStream text, byte[] name)
    {
        Write(text, "\t");
        text.Write(name);
        Write(text, ":");
    }

    private static bool IsBinary(byte[] name, byte[] value) =>
        (name.Length > BinarySuffix.Length && Encoding.Latin1.GetString(name).EndsWith(BinarySuffix, StringComparison.OrdinalIgnoreCase))
        || !IsPrintable(value);

    private static bool IsPrintable(byte[] value) =>
        value.Length == 0
        || (!IsBlank(value[0]) && !IsBlank(value[^1]) && Array.TrueForAll(value, IsPrintableOrSpace));

    private static bool IsBlank(byte octet) => octet is (byte)' ' or (byte)'\t';

    private static bool IsPrintableOrSpace(byte octet) => octet is (>= 0x20 and <= 0x7E) or (>= 0x09 and <= 0x0D);

    private static byte[] Base64(byte[] value) => Encoding.ASCII.GetBytes(Convert.ToBase64String(value));

    private static void Write(MemoryStream text, string ascii) => text.Write(Encoding.ASCII.GetBytes(ascii));
}
