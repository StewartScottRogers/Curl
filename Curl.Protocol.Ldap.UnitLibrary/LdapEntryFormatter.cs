using System.Text;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Formats one SearchResultEntry as each reference build writes it to the output (ADR-0166,
/// measured by BL-588 against curl 8.21.0 with WinLDAP and curl 8.18.0 with OpenLDAP 2.6.10),
/// in the pieces curl hands its writer one at a time (measured by BL-845).
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
/// <para>
/// The pieces are those of curl's <c>lib/ldap.c</c> and <c>lib/openldap.c</c>, each one
/// <c>Curl_client_write</c>: <c>DN: </c>, the DN, the line end; per value the tab, the name,
/// the colon, the separator, the value and the line end; the blank line after each attribute,
/// and for OpenLDAP after the entry. A write to an output that fails is reported with the size
/// of the piece it failed on, so the pieces are what <c>passed N</c> counts. A piece may be
/// empty; curl's writer is never called for one.
/// </para>
/// </remarks>
internal static class LdapEntryFormatter
{
    private const string BinarySuffix = ";binary";

    /// <summary>Gets the pieces <paramref name="dialect" />'s build writes for <paramref name="entry" />, in order.</summary>
    /// <param name="dialect">The build to answer as.</param>
    /// <param name="entry">
    /// The entry; for <see cref="LdapDialect.OpenLdap" />, one whose attributes could be read,
    /// since that build fails on any other before writing it.
    /// </param>
    /// <returns>The pieces to write, each as curl hands it to its writer; some may be empty.</returns>
    public static IReadOnlyList<byte[]> FormatPieces(LdapDialect dialect, LdapSearchEntry entry)
    {
        var pieces = new List<byte[]>();
        IReadOnlyList<LdapEntryAttribute> attributes = entry.Attributes ?? [];
        if (dialect == LdapDialect.WinLdap)
        {
            AddWinLdap(pieces, entry.Dn, attributes);
        }
        else
        {
            AddOpenLdap(pieces, entry.Dn, attributes);
        }

        return pieces;
    }

    private static void AddWinLdap(List<byte[]> pieces, byte[] dn, IReadOnlyList<LdapEntryAttribute> attributes)
    {
        Add(pieces, "DN: ");
        pieces.Add(LdapWireText.ToWindowsAnsi(dn));
        Add(pieces, "\n");
        foreach (LdapEntryAttribute attribute in attributes)
        {
            byte[] name = LdapWireText.ToWindowsAnsi(attribute.Name);
            IReadOnlyList<byte[]> values = LdapWireText.SurvivesWindowsAnsi(attribute.Name) ? attribute.Values : [];
            foreach (byte[] value in values)
            {
                AddNameAndColon(pieces, name);
                bool binary = IsBinary(name, value);
                Add(pieces, binary ? ": " : " ");
                pieces.Add(binary ? Base64(value) : value);
                Add(pieces, "\n");
            }

            Add(pieces, "\n");
        }
    }

    private static void AddOpenLdap(List<byte[]> pieces, byte[] dn, IReadOnlyList<LdapEntryAttribute> attributes)
    {
        AddSpaced(pieces, "DN: ", dn);
        Add(pieces, "\n");
        foreach (LdapEntryAttribute attribute in attributes)
        {
            if (attribute.Values.Count == 0)
            {
                Add(pieces, "\t");
                pieces.Add(attribute.Name);
                Add(pieces, ":\n");
                continue;
            }

            foreach (byte[] value in attribute.Values)
            {
                AddNameAndColon(pieces, attribute.Name);
                bool binary = IsBinary(attribute.Name, value);
                AddSpaced(pieces, binary ? ": " : " ", binary ? Base64(value) : value);
                Add(pieces, "\n");
            }

            Add(pieces, "\n");
        }

        Add(pieces, "\n");
    }

    /// <summary>
    /// Adds <paramref name="prefix" /> and <paramref name="value" />, the prefix without its
    /// closing space when the value is empty, as <c>libldap</c>'s build does.
    /// </summary>
    private static void AddSpaced(List<byte[]> pieces, string prefix, byte[] value)
    {
        Add(pieces, value.Length > 0 ? prefix : prefix.TrimEnd(' '));
        pieces.Add(value);
    }

    private static void AddNameAndColon(List<byte[]> pieces, byte[] name)
    {
        Add(pieces, "\t");
        pieces.Add(name);
        Add(pieces, ":");
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

    private static void Add(List<byte[]> pieces, string ascii) => pieces.Add(Encoding.ASCII.GetBytes(ascii));
}
