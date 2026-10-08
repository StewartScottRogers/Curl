using System.Buffers;
using System.Formats.Asn1;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Encodes a search filter's string form (RFC 4515) as the Filter of a SearchRequest
/// (RFC 4511 section 4.5.1.7), the way each reference build's filter parser does
/// (ADR-0166, measured by BL-587): WinLDAP's on Windows, <c>libldap</c>'s elsewhere.
/// </summary>
/// <remarks>
/// <para>
/// Both builds read <c>(&amp;...)</c>, <c>(|...)</c>, <c>(!...)</c>, <c>=</c>, <c>~=</c>,
/// <c>&gt;=</c>, <c>&lt;=</c>, presence (<c>=*</c>), substrings and extensible matches
/// (<c>attr:dn:rule:=value</c>), skip blanks after <c>(</c> and between items, take a filter
/// without parentheses as one item, and decode <c>\XX</c> in values.
/// </para>
/// <para>
/// Where they differ: WinLDAP encodes every top-level item of <c>(a=1)(b=2)</c> and every
/// item under <c>!</c>, refuses an empty <c>&amp;</c> or <c>|</c> and an empty equality value,
/// keeps a <c>\</c> that is not a hex escape as it is, skips empty substrings, writes the
/// whole text between the first <c>:</c> and <c>:=</c> as the matching rule (so
/// <c>dn:1.2</c>), and always writes <c>dnAttributes</c>. <c>libldap</c> refuses a second
/// top-level item and a <c>!</c> with two, accepts <c>(&amp;)</c> and <c>(|)</c> and an empty
/// value, reads <c>\(</c>, <c>\)</c>, <c>\*</c> and <c>\\</c> as the character and refuses any
/// other <c>\</c>, refuses empty substrings and attribute names outside RFC 4512's
/// characters, and writes <c>dnAttributes</c> only when it is true.
/// </para>
/// </remarks>
/// <param name="dialect">The build whose parser to follow.</param>
/// <param name="writer">Writes constructed elements with the dialect's length form.</param>
internal sealed class LdapFilterEncoder(LdapDialect dialect, LdapBerWriter writer)
{
    /// <summary>The most <c>&amp;</c>, <c>|</c> and <c>!</c> sets one filter nests, so a deep filter cannot overflow the stack (ADR-0425).</summary>
    internal const int MaximumSetDepth = 256;

    private static readonly Asn1Tag AndTag = new(TagClass.ContextSpecific, 0, isConstructed: true);

    private static readonly Asn1Tag OrTag = new(TagClass.ContextSpecific, 1, isConstructed: true);

    private static readonly Asn1Tag NotTag = new(TagClass.ContextSpecific, 2, isConstructed: true);

    private static readonly Asn1Tag EqualityTag = new(TagClass.ContextSpecific, 3, isConstructed: true);

    private static readonly Asn1Tag SubstringsTag = new(TagClass.ContextSpecific, 4, isConstructed: true);

    private static readonly Asn1Tag GreaterOrEqualTag = new(TagClass.ContextSpecific, 5, isConstructed: true);

    private static readonly Asn1Tag LessOrEqualTag = new(TagClass.ContextSpecific, 6, isConstructed: true);

    private static readonly Asn1Tag PresentTag = new(TagClass.ContextSpecific, 7);

    private static readonly Asn1Tag ApproximateTag = new(TagClass.ContextSpecific, 8, isConstructed: true);

    private static readonly Asn1Tag ExtensibleTag = new(TagClass.ContextSpecific, 9, isConstructed: true);

    /// <summary>The characters RFC 4515 requires a value to escape.</summary>
    private static readonly SearchValues<char> SpecialCharacters = SearchValues.Create("()*\\");

    /// <summary>The characters <c>libldap</c> takes in an attribute description or matching rule.</summary>
    private static readonly SearchValues<char> AttributeCharacters =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-;.");

    private readonly bool isWinLdap = dialect == LdapDialect.WinLdap;

    private string text = string.Empty;

    private int position;

    /// <summary>How many sets enclose the position.</summary>
    private int setDepth;

    /// <summary>Gets the fewest items <c>&amp;</c> and <c>|</c> take: one for WinLDAP, none for <c>libldap</c>.</summary>
    private int EmptySetMinimum => isWinLdap ? 1 : 0;

    /// <summary>Gets the most items <c>!</c> takes: any number for WinLDAP, one for <c>libldap</c>.</summary>
    private int NotMaximum => isWinLdap ? int.MaxValue : 1;

    /// <summary>Gets the default filter, <c>(objectClass=*)</c>, as the build spells its attribute.</summary>
    /// <returns>The encoded presence filter.</returns>
    public byte[] EncodeDefault() =>
        LdapBerWriter.OctetString(PresentTag, LdapWireText.Encode(dialect, isWinLdap ? "ObjectClass" : "objectclass"));

    /// <summary>Encodes <paramref name="filter" />.</summary>
    /// <param name="filter">The filter's string form, as a byte string.</param>
    /// <returns>The encoded Filter elements - more than one only for WinLDAP's run of top-level items - or <see langword="null" /> when the build refuses the filter or it nests more than <see cref="MaximumSetDepth" /> sets.</returns>
    public byte[]? Encode(string filter)
    {
        text = filter;
        position = 0;
        setDepth = 0;
        return filter.StartsWith('(') ? ParseTopLevel() : EncodeItem(filter);
    }

    private static bool IsBetween(int count, int minimum, int maximum) => count >= minimum && count <= maximum;

    /// <summary>The substring's choice: 0 <c>initial</c> for the first piece, 2 <c>final</c> for the last, 1 <c>any</c> between.</summary>
    private static int SubstringChoiceOf(int index, int count) => index == 0 ? 0 : index == count - 1 ? 2 : 1;

    /// <summary>The matching rule among <c>attr[:dn][:rule]</c>'s parts; the empty string when the parts do not fit.</summary>
    private static string? OpenLdapRuleOf(string[] parts, bool dnAttributes) => parts.Length switch
    {
        1 => null,
        2 => dnAttributes ? null : parts[1],
        _ => dnAttributes ? parts[2] : string.Empty,
    };

    private static bool IsDn(string text) => text.Equals("dn", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Decodes as <c>libldap</c> does: <c>\</c> before a special character is that character;
    /// any other <c>\</c>, and a special character on its own, is refused.
    /// </summary>
    /// <returns>How many characters it took, or -1 when <c>libldap</c> refuses it.</returns>
    private static int DecodeOpenLdapAt(string value, int index, List<byte> bytes)
    {
        bool escapesSpecial = EscapesSpecial(value, index);
        if (!escapesSpecial && SpecialCharacters.Contains(value[index]))
        {
            return -1;
        }

        bytes.Add((byte)value[escapesSpecial ? index + 1 : index]);
        return escapesSpecial ? 2 : 1;
    }

    /// <summary>Whether a <c>\</c> at <paramref name="index" /> escapes a special character after it.</summary>
    private static bool EscapesSpecial(string value, int index) =>
        value[index] == '\\' && index + 1 < value.Length && SpecialCharacters.Contains(value[index + 1]);

    /// <summary>Parses the top-level parenthesized items: any number for WinLDAP, one for <c>libldap</c>.</summary>
    private byte[]? ParseTopLevel()
    {
        List<byte[]> filters = [];
        while (SkipBlanks())
        {
            byte[]? next = filters.Count == 0 || isWinLdap ? ParseParenthesized() : null;
            if (next is null)
            {
                return null;
            }

            filters.Add(next);
        }

        return [.. filters.SelectMany(element => element)];
    }

    /// <summary>Skips blanks.</summary>
    /// <returns><see langword="true" /> while text remains.</returns>
    private bool SkipBlanks()
    {
        while (position < text.Length && text[position] == ' ')
        {
            position++;
        }

        return position < text.Length;
    }

    /// <summary>Parses the parenthesized filter at the position, which must be a <c>(</c>.</summary>
    private byte[]? ParseParenthesized()
    {
        if (text[position] != '(')
        {
            return null;
        }

        position++;
        return SkipBlanks() ? ParseAfterParenthesis() : null;
    }

    /// <summary>Parses what follows a <c>(</c>: a set by its operator, or a simple item.</summary>
    private byte[]? ParseAfterParenthesis() => text[position] switch
    {
        '&' => ParseNestedSet(AndTag, EmptySetMinimum, int.MaxValue),
        '|' => ParseNestedSet(OrTag, EmptySetMinimum, int.MaxValue),
        '!' => ParseNestedSet(NotTag, 1, NotMaximum),
        _ => ParseSimple(),
    };

    /// <summary>Parses a set one level deeper, refusing it past <see cref="MaximumSetDepth" /> (ADR-0425).</summary>
    private byte[]? ParseNestedSet(Asn1Tag tag, int minimum, int maximum) =>
        ++setDepth > MaximumSetDepth ? null : ParseSet(tag, minimum, maximum);

    /// <summary>Parses the items of <c>&amp;</c>, <c>|</c> or <c>!</c> up to its <c>)</c>.</summary>
    private byte[]? ParseSet(Asn1Tag tag, int minimum, int maximum)
    {
        position++;
        List<byte[]> items = [];
        while (SkipBlanks() && text[position] != ')')
        {
            byte[]? item = ParseParenthesized();
            if (item is null)
            {
                return null;
            }

            items.Add(item);
        }

        return position < text.Length && IsBetween(items.Count, minimum, maximum) ? CloseSet(tag, items) : null;
    }

    private byte[] CloseSet(Asn1Tag tag, List<byte[]> items)
    {
        position++;
        setDepth--;
        return writer.Constructed(tag, [.. items]);
    }

    /// <summary>Parses a simple item up to its <c>)</c>; a <c>\</c> hides the character after it.</summary>
    private byte[]? ParseSimple()
    {
        int start = position;
        while (position < text.Length && text[position] != ')')
        {
            position += text[position] == '\\' ? 2 : 1;
        }

        if (position >= text.Length)
        {
            return null;
        }

        position++;
        return EncodeItem(text[start..(position - 1)]);
    }

    /// <summary>Encodes one item, such as <c>cn=a</c>, by the operator before its first <c>=</c>.</summary>
    private byte[]? EncodeItem(string item)
    {
        int equals = item.IndexOf('=', StringComparison.Ordinal);
        return equals > 0 ? EncodeOperator(item[equals - 1], item[..equals], item[(equals + 1)..]) : null;
    }

    /// <summary>Encodes an item by <paramref name="operatorCharacter" />, the character before its <c>=</c>.</summary>
    /// <param name="operatorCharacter">The character before the <c>=</c>.</param>
    /// <param name="left">The text before the <c>=</c>, the operator character included.</param>
    /// <param name="value">The text after the <c>=</c>.</param>
    private byte[]? EncodeOperator(char operatorCharacter, string left, string value) => operatorCharacter switch
    {
        '~' => EncodeAssertion(ApproximateTag, left[..^1], value),
        '>' => EncodeAssertion(GreaterOrEqualTag, left[..^1], value),
        '<' => EncodeAssertion(LessOrEqualTag, left[..^1], value),
        ':' => EncodeExtensible(left[..^1], value),
        _ => EncodeEqualityOrSubstrings(left, value),
    };

    private byte[]? EncodeAssertion(Asn1Tag tag, string attribute, string value)
    {
        byte[]? assertion = IsAttribute(attribute) ? DecodeValue(value) : null;
        return assertion is null ? null : writer.Constructed(tag, OctetString(attribute), LdapBerWriter.OctetString(Asn1Tag.PrimitiveOctetString, assertion));
    }

    private byte[]? EncodeEqualityOrSubstrings(string attribute, string value)
    {
        if (!IsAttribute(attribute))
        {
            return null;
        }

        if (value == "*")
        {
            return LdapBerWriter.OctetString(PresentTag, LdapWireText.Encode(dialect, attribute));
        }

        List<string> pieces = SplitAtWildcards(value);
        return pieces.Count > 1 ? EncodeSubstrings(attribute, pieces) : EncodeEquality(attribute, value);
    }

    /// <summary>Encodes an equality match; WinLDAP refuses an empty value, <c>libldap</c> sends it.</summary>
    private byte[]? EncodeEquality(string attribute, string value)
    {
        byte[]? assertion = value.Length > 0 || !isWinLdap ? DecodeValue(value) : null;
        return assertion is null ? null : writer.Constructed(EqualityTag, OctetString(attribute), LdapBerWriter.OctetString(Asn1Tag.PrimitiveOctetString, assertion));
    }

    /// <summary>Encodes the initial, any and final substrings between the wildcards.</summary>
    private byte[]? EncodeSubstrings(string attribute, List<string> pieces)
    {
        List<byte[]> substrings = [];
        for (int index = 0; index < pieces.Count; index++)
        {
            if (!TryAddSubstring(substrings, pieces[index], SubstringChoiceOf(index, pieces.Count)))
            {
                return null;
            }
        }

        return writer.Constructed(SubstringsTag, OctetString(attribute), writer.Constructed(Asn1Tag.Sequence, [.. substrings]));
    }

    /// <summary>
    /// Adds one piece as a substring. An empty initial or final piece is left out; an empty
    /// piece between wildcards is left out by WinLDAP and refused by <c>libldap</c>.
    /// </summary>
    /// <returns><see langword="false" /> when the build refuses the piece.</returns>
    private bool TryAddSubstring(List<byte[]> substrings, string piece, int choice)
    {
        if (piece.Length == 0)
        {
            return choice != 1 || isWinLdap;
        }

        byte[]? bytes = DecodeValue(piece);
        if (bytes is null)
        {
            return false;
        }

        substrings.Add(LdapBerWriter.OctetString(new Asn1Tag(TagClass.ContextSpecific, choice), bytes));
        return true;
    }

    private byte[]? EncodeExtensible(string left, string value) =>
        isWinLdap ? EncodeWinLdapExtensible(left, value) : EncodeOpenLdapExtensible(left, value);

    /// <summary>
    /// Encodes <c>attr:rest:=value</c> as WinLDAP does: the attribute is the text before the
    /// first <c>:</c>, the rest is the matching rule unless it is just <c>dn</c>, and
    /// <c>dnAttributes</c> is true when the rest is or starts with <c>dn</c>.
    /// </summary>
    private byte[]? EncodeWinLdapExtensible(string left, string value)
    {
        string[] halves = left.Split(':', 2);
        string rest = halves.Length == 2 ? halves[1] : string.Empty;
        bool dnOnly = IsDn(rest);
        bool dnAttributes = dnOnly || rest.StartsWith("dn:", StringComparison.OrdinalIgnoreCase);
        string? rule = rest.Length == 0 || dnOnly ? null : rest;
        return WriteExtensible(new ExtensibleMatch(rule, halves[0], value, dnAttributes), writeFalse: true);
    }

    /// <summary>
    /// Encodes <c>attr[:dn][:rule]:=value</c> as <c>libldap</c> does: a second part that is
    /// <c>dn</c> sets <c>dnAttributes</c>, any other is the rule, and an attribute, a rule or
    /// both must be given.
    /// </summary>
    private byte[]? EncodeOpenLdapExtensible(string left, string value)
    {
        string[] parts = left.Split(':');
        if (parts.Length > 3)
        {
            return null;
        }

        bool dnAttributes = parts.Length > 1 && IsDn(parts[1]);
        string? rule = OpenLdapRuleOf(parts, dnAttributes);
        return IsOpenLdapExtensible(parts[0], rule)
            ? WriteExtensible(new ExtensibleMatch(rule, parts[0], value, dnAttributes), writeFalse: false)
            : null;
    }

    private bool IsOpenLdapExtensible(string attribute, string? rule) =>
        rule is null ? IsAttribute(attribute) : IsAttribute(rule) && (attribute.Length == 0 || IsAttribute(attribute));

    /// <summary>Writes a MatchingRuleAssertion (RFC 4511 section 4.5.1.7.7).</summary>
    /// <param name="match">What to write.</param>
    /// <param name="writeFalse">Whether to write <c>dnAttributes</c> when it is false, as WinLDAP does.</param>
    private byte[]? WriteExtensible(ExtensibleMatch match, bool writeFalse)
    {
        byte[]? assertion = DecodeValue(match.Value);
        if (assertion is null)
        {
            return null;
        }

        List<byte[]> elements = [];
        AddIfGiven(elements, 1, match.Rule);
        AddIfGiven(elements, 2, match.Attribute);
        elements.Add(LdapBerWriter.OctetString(new Asn1Tag(TagClass.ContextSpecific, 3), assertion));
        if (match.DnAttributes || writeFalse)
        {
            elements.Add(LdapBerWriter.OctetString(new Asn1Tag(TagClass.ContextSpecific, 4), [match.DnAttributes ? (byte)0xFF : (byte)0x00]));
        }

        return writer.Constructed(ExtensibleTag, [.. elements]);
    }

    private void AddIfGiven(List<byte[]> elements, int choice, string? given)
    {
        if (!string.IsNullOrEmpty(given))
        {
            elements.Add(LdapBerWriter.OctetString(new Asn1Tag(TagClass.ContextSpecific, choice), LdapWireText.Encode(dialect, given)));
        }
    }

    /// <summary>
    /// Splits a value at its wildcards: every <c>*</c> for WinLDAP, every <c>*</c> not after a
    /// <c>\</c> for <c>libldap</c>.
    /// </summary>
    private List<string> SplitAtWildcards(string value)
    {
        List<string> pieces = [];
        int start = 0;
        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] == '\\' && !isWinLdap)
            {
                index++;
            }
            else if (value[index] == '*')
            {
                pieces.Add(value[start..index]);
                start = index + 1;
            }
        }

        pieces.Add(value[start..]);
        return pieces;
    }

    /// <summary>Decodes a value's escapes into the bytes sent.</summary>
    /// <returns>The bytes, or <see langword="null" /> when <c>libldap</c> refuses an escape or a bare special character.</returns>
    private byte[]? DecodeValue(string value)
    {
        List<byte> bytes = [];
        for (int index = 0; index < value.Length;)
        {
            int consumed = DecodeAt(value, index, bytes);
            if (consumed < 0)
            {
                return null;
            }

            index += consumed;
        }

        return [.. bytes];
    }

    /// <summary>Decodes the character or escape at <paramref name="index" /> into <paramref name="bytes" />.</summary>
    /// <returns>How many characters it took, or -1 when <c>libldap</c> refuses it.</returns>
    private int DecodeAt(string value, int index, List<byte> bytes)
    {
        if (LdapHexEscape.TryRead(value, index, '\\', out char octet))
        {
            bytes.Add((byte)octet);
            return 3;
        }

        if (isWinLdap)
        {
            bytes.AddRange(LdapWireText.Encode(dialect, value[index].ToString()));
            return 1;
        }

        return DecodeOpenLdapAt(value, index, bytes);
    }

    /// <summary>
    /// Whether the build takes <paramref name="attribute" /> as an attribute description:
    /// WinLDAP any non-empty text, <c>libldap</c> only letters, digits, <c>-</c>, <c>;</c> and <c>.</c>.
    /// </summary>
    private bool IsAttribute(string attribute) =>
        attribute.Length > 0 && (isWinLdap || !attribute.AsSpan().ContainsAnyExcept(AttributeCharacters));

    private byte[] OctetString(string attribute) =>
        LdapBerWriter.OctetString(Asn1Tag.PrimitiveOctetString, LdapWireText.Encode(dialect, attribute));

    /// <summary>A MatchingRuleAssertion's parts.</summary>
    /// <param name="Rule">The matching rule, or <see langword="null" />.</param>
    /// <param name="Attribute">The attribute; empty when none is given.</param>
    /// <param name="Value">The assertion value, escapes undecoded.</param>
    /// <param name="DnAttributes">Whether the DN's attributes are matched too.</param>
    private sealed record ExtensibleMatch(string? Rule, string Attribute, string Value, bool DnAttributes);
}
