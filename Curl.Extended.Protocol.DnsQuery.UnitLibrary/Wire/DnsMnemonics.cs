using System.Globalization;

namespace Curl.Extended.Protocol.DnsQuery.Wire;

/// <summary>
/// Turns record types, classes, opcodes and response codes into their mnemonics and back, with RFC 3597's
/// <c>TYPE</c><i>n</i> and <c>CLASS</c><i>n</i> for a value that has none.
/// </summary>
internal static class DnsMnemonics
{
    private const string TypePrefix = "TYPE";
    private const string ClassPrefix = "CLASS";

    private static readonly (ushort Value, string Mnemonic)[] Types =
    [
        (1, "A"), (2, "NS"), (5, "CNAME"), (6, "SOA"), (12, "PTR"), (15, "MX"), (16, "TXT"), (28, "AAAA"),
        (33, "SRV"), (41, "OPT"), (252, "AXFR"), (255, "ANY"), (257, "CAA"),
    ];

    private static readonly (ushort Value, string Mnemonic)[] Classes =
    [
        (1, "IN"), (3, "CH"), (4, "HS"), (254, "NONE"), (255, "ANY"),
    ];

    private static readonly (ushort Value, string Mnemonic)[] Opcodes =
    [
        (0, "QUERY"), (1, "IQUERY"), (2, "STATUS"), (4, "NOTIFY"), (5, "UPDATE"),
    ];

    private static readonly (ushort Value, string Mnemonic)[] ResponseCodes =
    [
        (0, "NOERROR"), (1, "FORMERR"), (2, "SERVFAIL"), (3, "NXDOMAIN"), (4, "NOTIMP"), (5, "REFUSED"),
    ];

    /// <summary>Gives a record type's mnemonic: <c>A</c> for 1, <c>TYPE65</c> for 65.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The mnemonic.</returns>
    public static string TypeMnemonic(ushort type) => Mnemonic(Types, type, TypePrefix);

    /// <summary>Gives a class's mnemonic: <c>IN</c> for 1, <c>CLASS2</c> for 2.</summary>
    /// <param name="recordClass">The class.</param>
    /// <returns>The mnemonic.</returns>
    public static string ClassMnemonic(ushort recordClass) => Mnemonic(Classes, recordClass, ClassPrefix);

    /// <summary>Gives an opcode's mnemonic: <c>QUERY</c> for 0, <c>OPCODE3</c> for 3.</summary>
    /// <param name="opcode">The opcode.</param>
    /// <returns>The mnemonic.</returns>
    public static string OpcodeMnemonic(int opcode) => Mnemonic(Opcodes, (ushort)opcode, "OPCODE");

    /// <summary>Gives a response code's mnemonic: <c>NXDOMAIN</c> for 3, <c>RCODE9</c> for 9.</summary>
    /// <param name="responseCode">The response code.</param>
    /// <returns>The mnemonic.</returns>
    public static string ResponseCodeMnemonic(int responseCode) => Mnemonic(ResponseCodes, (ushort)responseCode, "RCODE");

    /// <summary>Reads a record type's mnemonic or <c>TYPE</c><i>n</i>, in either case.</summary>
    /// <param name="text">The mnemonic.</param>
    /// <param name="type">The type, or 0 when the text names none.</param>
    /// <returns><see langword="true" /> when the text names a type.</returns>
    public static bool TryParseType(string text, out ushort type) => TryParse(Types, text, TypePrefix, out type);

    /// <summary>Reads a class's mnemonic or <c>CLASS</c><i>n</i>, in either case.</summary>
    /// <param name="text">The mnemonic.</param>
    /// <param name="recordClass">The class, or 0 when the text names none.</param>
    /// <returns><see langword="true" /> when the text names a class.</returns>
    public static bool TryParseClass(string text, out ushort recordClass) => TryParse(Classes, text, ClassPrefix, out recordClass);

    private static string Mnemonic((ushort Value, string Mnemonic)[] table, ushort value, string prefix)
    {
        foreach ((ushort known, string mnemonic) in table)
        {
            if (known == value)
            {
                return mnemonic;
            }
        }

        return prefix + value.ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryParse((ushort Value, string Mnemonic)[] table, string text, string prefix, out ushort value)
    {
        foreach ((ushort known, string mnemonic) in table)
        {
            if (string.Equals(mnemonic, text, StringComparison.OrdinalIgnoreCase))
            {
                value = known;
                return true;
            }
        }

        value = 0;
        return text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && ushort.TryParse(text.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
