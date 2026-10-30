using System.Globalization;

namespace Curl.Extension.StructuredFields;

/// <summary>
/// Writes a parsed Structured Field value as one readable line that names every type (ADR-1152): a List as
/// <c>List of 2: Token a; Integer 1 [q=Decimal 0.5]</c>, a Dictionary as <c>Dictionary of 1: u=Integer 3</c>, an Item
/// as its bare item and parameters, an Inner List as <c>Inner List (Token a, Token b)</c>.
/// </summary>
internal static class ReadableStructuredValue
{
    /// <summary>Describes a List (RFC 9651 §3.1).</summary>
    /// <param name="members">The List's members.</param>
    /// <returns>The readable line.</returns>
    public static string OfList(IReadOnlyList<ListMember> members) =>
        Counted("List", members.Select(OfMember).ToList());

    /// <summary>Describes a Dictionary (RFC 9651 §3.2).</summary>
    /// <param name="members">The Dictionary's members.</param>
    /// <returns>The readable line.</returns>
    public static string OfDictionary(IReadOnlyList<DictionaryMember> members) =>
        Counted("Dictionary", members.Select(member => member.Key + "=" + OfMember(member.Value)).ToList());

    /// <summary>Describes an Item (RFC 9651 §3.3) and its parameters.</summary>
    /// <param name="item">The Item.</param>
    /// <returns>The readable text.</returns>
    public static string OfItem(Item item) => OfBareItem(item.Value) + OfParameters(item.Parameters);

    private static string Counted(string type, List<string> members) =>
        $"{type} of {members.Count}" + (members.Count == 0 ? string.Empty : ": " + string.Join("; ", members));

    private static string OfMember(ListMember member) => member is InnerList list
        ? "Inner List (" + string.Join(", ", list.Items.Select(OfItem)) + ")" + OfParameters(list.Parameters)
        : OfItem((Item)member);

    private static string OfParameters(IReadOnlyList<Parameter> parameters) => parameters.Count == 0
        ? string.Empty
        : " [" + string.Join(", ", parameters.Select(parameter => parameter.Key + "=" + OfBareItem(parameter.Value))) + "]";

    private static string OfBareItem(BareItem item) => item.Type switch
    {
        BareItemType.Integer => "Integer " + ((long)item.Value).ToString(CultureInfo.InvariantCulture),
        BareItemType.Decimal => "Decimal " + ((decimal)item.Value).ToString("0.0##", CultureInfo.InvariantCulture),
        BareItemType.String => "String " + Quoted((string)item.Value),
        BareItemType.Token => "Token " + (string)item.Value,
        BareItemType.ByteSequence => OfBytes((byte[])item.Value),
        BareItemType.Boolean => (bool)item.Value ? "Boolean true" : "Boolean false",
        BareItemType.Date => "Date " + OfDate((long)item.Value),
        _ => "Display String " + Quoted((string)item.Value),
    };

    private static string OfBytes(byte[] bytes) =>
        $"Byte Sequence 0x{Convert.ToHexStringLower(bytes)} ({bytes.Length} bytes)";

    private static string OfDate(long seconds) =>
        seconds is >= -62135596800 and <= 253402300799
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
            : "@" + seconds.ToString(CultureInfo.InvariantCulture);

    private static string Quoted(string text) => "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
