namespace Curl.Extended.Protocol.DnsQuery.Wire;

/// <summary>One named field of a record's RDATA.</summary>
/// <param name="Name">The field's name, after the RFC that defines the record type (<c>preference</c>, <c>exchange</c>).</param>
/// <param name="Kind">How the field is presented.</param>
/// <param name="Values">The field's presentation text: one value, or one per character-string.</param>
internal sealed record DnsRecordField(string Name, DnsFieldKind Kind, IReadOnlyList<string> Values)
{
    /// <summary>Gives a number field.</summary>
    /// <param name="name">The field's name.</param>
    /// <param name="value">The number.</param>
    /// <returns>The field.</returns>
    public static DnsRecordField Number(string name, uint value) =>
        new(name, DnsFieldKind.Number, [value.ToString(System.Globalization.CultureInfo.InvariantCulture)]);

    /// <summary>Gives a text field.</summary>
    /// <param name="name">The field's name.</param>
    /// <param name="value">The presentation text.</param>
    /// <returns>The field.</returns>
    public static DnsRecordField Text(string name, string value) => new(name, DnsFieldKind.Text, [value]);
}
