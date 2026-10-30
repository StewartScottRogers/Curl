namespace Curl.Extension.StructuredFields;

/// <summary>The eight bare item types of RFC 9651 §3.3.</summary>
internal enum BareItemType
{
    /// <summary>An Integer (§3.3.1), held as a <see cref="long" />.</summary>
    Integer,

    /// <summary>A Decimal (§3.3.2), held as a <see cref="decimal" />.</summary>
    Decimal,

    /// <summary>A String (§3.3.3), held as a <see cref="string" />.</summary>
    String,

    /// <summary>A Token (§3.3.4), held as a <see cref="string" />.</summary>
    Token,

    /// <summary>A Byte Sequence (§3.3.5), held as a <see cref="byte" /> array.</summary>
    ByteSequence,

    /// <summary>A Boolean (§3.3.6), held as a <see cref="bool" />.</summary>
    Boolean,

    /// <summary>A Date (§3.3.7), held as its seconds since the Unix epoch, a <see cref="long" />.</summary>
    Date,

    /// <summary>A Display String (§3.3.8), held as the decoded <see cref="string" />.</summary>
    DisplayString,
}

/// <summary>A bare item (RFC 9651 §3.3): its type and its value.</summary>
/// <param name="Type">The bare item's type.</param>
/// <param name="Value">The value, of the CLR type <see cref="BareItemType" /> names for <paramref name="Type" />.</param>
internal sealed record BareItem(BareItemType Type, object Value);

/// <summary>One parameter (RFC 9651 §3.1.2): a key and a bare item.</summary>
/// <param name="Key">The parameter's key.</param>
/// <param name="Value">The parameter's value; <c>?1</c> when the field gave only the key.</param>
internal sealed record Parameter(string Key, BareItem Value);

/// <summary>A member of a List or a Dictionary: an Item or an Inner List, each with its parameters.</summary>
/// <param name="Parameters">The member's parameters, in the order the field gave them.</param>
internal abstract record ListMember(IReadOnlyList<Parameter> Parameters);

/// <summary>An Item (RFC 9651 §3.3): a bare item and its parameters.</summary>
/// <param name="Value">The bare item.</param>
/// <param name="Parameters">The item's parameters.</param>
internal sealed record Item(BareItem Value, IReadOnlyList<Parameter> Parameters) : ListMember(Parameters);

/// <summary>An Inner List (RFC 9651 §3.1.1): Items and the list's own parameters.</summary>
/// <param name="Items">The inner list's items.</param>
/// <param name="Parameters">The inner list's parameters.</param>
internal sealed record InnerList(IReadOnlyList<Item> Items, IReadOnlyList<Parameter> Parameters) : ListMember(Parameters);

/// <summary>One Dictionary member (RFC 9651 §3.2): a key and an Item or Inner List.</summary>
/// <param name="Key">The member's key.</param>
/// <param name="Value">The member's value; the Boolean true Item when the field gave only the key.</param>
internal sealed record DictionaryMember(string Key, ListMember Value);
