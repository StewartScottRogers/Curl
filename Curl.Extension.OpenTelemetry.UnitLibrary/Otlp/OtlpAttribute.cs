namespace Curl.Extension.OpenTelemetry.Otlp;

/// <summary>One OTLP <c>KeyValue</c> whose <c>AnyValue</c> is a string or an integer (opentelemetry-proto <c>common/v1/common.proto</c>).</summary>
/// <param name="Key">The attribute name.</param>
/// <param name="StringValue">The string value, or <see langword="null" /> when the value is <paramref name="IntValue" />.</param>
/// <param name="IntValue">The integer value, read when <paramref name="StringValue" /> is <see langword="null" />.</param>
internal sealed record OtlpAttribute(string Key, string? StringValue, long IntValue = 0)
{
    /// <summary>Gives a string attribute.</summary>
    /// <param name="key">The attribute name.</param>
    /// <param name="value">The value.</param>
    /// <returns>The attribute.</returns>
    internal static OtlpAttribute String(string key, string value) => new(key, value);

    /// <summary>Gives an integer attribute.</summary>
    /// <param name="key">The attribute name.</param>
    /// <param name="value">The value.</param>
    /// <returns>The attribute.</returns>
    internal static OtlpAttribute Int(string key, long value) => new(key, null, value);
}
