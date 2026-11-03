using System.Text;

namespace Curl.Conformance.Extended.Kafka.Records;

/// <summary>One record of a batch, as record batch v2 holds it (its attributes byte is unused and written 0).</summary>
/// <param name="OffsetDelta">Its offset less the batch's base offset.</param>
/// <param name="TimestampDelta">Its timestamp less the batch's base timestamp, which may be negative.</param>
/// <param name="Key">Its key, or null.</param>
/// <param name="Value">Its value, or null.</param>
/// <param name="Headers">Its headers, each a key and a value or null.</param>
public sealed record KafkaReferenceRecord(int OffsetDelta, long TimestampDelta, byte[]? Key, byte[]? Value, IReadOnlyList<KeyValuePair<string, byte[]?>> Headers)
{
    /// <summary>A record with a value only: no key, no headers, the batch's timestamp.</summary>
    /// <param name="offsetDelta">Its offset delta.</param>
    /// <param name="value">Its value as UTF-8.</param>
    /// <returns>The record.</returns>
    public static KafkaReferenceRecord Plain(int offsetDelta, string value) => new(offsetDelta, 0, null, Encoding.UTF8.GetBytes(value), []);
}
