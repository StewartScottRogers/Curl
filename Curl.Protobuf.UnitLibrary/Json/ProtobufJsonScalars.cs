using System.Globalization;
using System.Text.Json;
using Curl.Protobuf.Descriptors;
using Curl.Protobuf.DynamicMessages;

namespace Curl.Protobuf.Json;

/// <summary>
/// Reads and writes the scalar and enum values of the proto3 JSON mapping: 64-bit integers as
/// strings, bytes as base64, enums by name, and <c>NaN</c>, <c>Infinity</c> and <c>-Infinity</c> as strings.
/// </summary>
internal static class ProtobufJsonScalars
{
    private const NumberStyles NumberSyntax = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

    private static readonly Dictionary<DynamicValueKind, IntegerRange> IntegerRanges = new()
    {
        [DynamicValueKind.Int32] = IntegerRange.Int32,
        [DynamicValueKind.SInt32] = IntegerRange.Int32,
        [DynamicValueKind.SFixed32] = IntegerRange.Int32,
        [DynamicValueKind.Int64] = IntegerRange.Int64,
        [DynamicValueKind.SInt64] = IntegerRange.Int64,
        [DynamicValueKind.SFixed64] = IntegerRange.Int64,
        [DynamicValueKind.UInt32] = IntegerRange.UInt32,
        [DynamicValueKind.Fixed32] = IntegerRange.UInt32,
        [DynamicValueKind.UInt64] = IntegerRange.UInt64,
        [DynamicValueKind.Fixed64] = IntegerRange.UInt64,
    };

    /// <summary>Reads a scalar of the kind from a JSON value; integers and floating point numbers are accepted as numbers or strings.</summary>
    public static ProtobufJsonError TryRead(JsonElement element, DynamicValueKind kind, out object? value)
    {
        switch (kind)
        {
            case DynamicValueKind.Double or DynamicValueKind.Float:
                return TryReadFloatingPoint(element, kind, out value);
            case DynamicValueKind.Bool:
                return TryReadBool(element, out value);
            case DynamicValueKind.String:
                return element.ValueKind == JsonValueKind.String
                    ? Succeed(element.GetString()!, out value)
                    : Fail(ProtobufJsonError.WrongType, out value);
            case DynamicValueKind.Bytes:
                return TryReadBytes(element, out value);
            default:
                return TryReadInteger(element, kind, out value);
        }
    }

    /// <summary>Reads an enum value from its name as a JSON string, or from its number.</summary>
    public static ProtobufJsonError TryReadEnum(JsonElement element, ProtoEnumDescriptor enumType, out object? value)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            return element.ValueKind == JsonValueKind.Number
                ? TryReadInteger(element, DynamicValueKind.Int32, out value)
                : Fail(ProtobufJsonError.WrongType, out value);
        }

        var name = element.GetString();
        value = enumType.Values.FirstOrDefault(enumValue => enumValue.Name == name)?.Number;
        return value is null ? ProtobufJsonError.InvalidValue : ProtobufJsonError.None;
    }

    /// <summary>Reads a map key of the kind from a JSON member name, where every key is a string.</summary>
    public static ProtobufJsonError TryReadMapKey(string text, DynamicValueKind kind, out object? key)
    {
        switch (kind)
        {
            case DynamicValueKind.String:
                key = text;
                return ProtobufJsonError.None;
            case DynamicValueKind.Bool:
                key = text switch { "true" => true, "false" => false, _ => null };
                return key is null ? ProtobufJsonError.InvalidValue : ProtobufJsonError.None;
            default:
                return TryParseInteger(text, kind, out key);
        }
    }

    /// <summary>The JSON member name of a map key.</summary>
    public static string MapKeyText(object key) => key is bool flag
        ? (flag ? "true" : "false")
        : Convert.ToString(key, CultureInfo.InvariantCulture)!;

    /// <summary>Writes a scalar: 64-bit integers as strings, bytes as base64, non-finite numbers by name.</summary>
    public static void Write(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case string text:
                writer.WriteStringValue(text);
                break;
            case byte[] bytes:
                writer.WriteBase64StringValue(bytes);
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case long or ulong:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
            default:
                WriteNumber(writer, value);
                break;
        }
    }

    /// <summary>Writes an enum value by its name, or by its number when the enum names none.</summary>
    public static void WriteEnum(Utf8JsonWriter writer, ProtoEnumDescriptor enumType, int number)
    {
        if (enumType.Values.FirstOrDefault(enumValue => enumValue.Number == number) is { } named)
        {
            writer.WriteStringValue(named.Name);
        }
        else
        {
            writer.WriteNumberValue(number);
        }
    }

    private static void WriteNumber(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case int number:
                writer.WriteNumberValue(number);
                break;
            case uint number:
                writer.WriteNumberValue(number);
                break;
            case float number:
                WriteSingle(writer, number);
                break;
            default:
                WriteDouble(writer, (double)value);
                break;
        }
    }

    private static void WriteSingle(Utf8JsonWriter writer, float number)
    {
        if (float.IsFinite(number))
        {
            writer.WriteNumberValue(number);
        }
        else
        {
            writer.WriteStringValue(NonFiniteName(number));
        }
    }

    private static void WriteDouble(Utf8JsonWriter writer, double number)
    {
        if (double.IsFinite(number))
        {
            writer.WriteNumberValue(number);
        }
        else
        {
            writer.WriteStringValue(NonFiniteName(number));
        }
    }

    private static ProtobufJsonError TryReadBool(JsonElement element, out object? value) => element.ValueKind switch
    {
        JsonValueKind.True => Succeed(true, out value),
        JsonValueKind.False => Succeed(false, out value),
        _ => Fail(ProtobufJsonError.WrongType, out value),
    };

    private static string NonFiniteName(double number) => double.IsNaN(number)
        ? "NaN"
        : number > 0 ? "Infinity" : "-Infinity";

    private static string? NumberText(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.String => element.GetString(),
        _ => null,
    };

    private static ProtobufJsonError TryReadInteger(JsonElement element, DynamicValueKind kind, out object? value) =>
        NumberText(element) is { } text ? TryParseInteger(text, kind, out value) : Fail(ProtobufJsonError.WrongType, out value);

    private static ProtobufJsonError TryParseInteger(string text, DynamicValueKind kind, out object? value)
    {
        var range = IntegerRanges[kind];
        var valid = decimal.TryParse(text, NumberSyntax, CultureInfo.InvariantCulture, out var number)
            && decimal.Truncate(number) == number
            && number >= range.Minimum
            && number <= range.Maximum;
        value = valid ? range.Convert(number) : null;
        return valid ? ProtobufJsonError.None : ProtobufJsonError.InvalidValue;
    }

    private static ProtobufJsonError TryReadFloatingPoint(JsonElement element, DynamicValueKind kind, out object? value)
    {
        if (NumberText(element) is not { } text)
        {
            return Fail(ProtobufJsonError.WrongType, out value);
        }

        if (!TryParseFloatingPoint(text, out var number))
        {
            return Fail(ProtobufJsonError.InvalidValue, out value);
        }

        if (kind == DynamicValueKind.Double)
        {
            return Succeed(number, out value);
        }

        // A finite double past float's range is out of range, not infinite.
        var single = (float)number;
        return float.IsFinite(single) || !double.IsFinite(number)
            ? Succeed(single, out value)
            : Fail(ProtobufJsonError.InvalidValue, out value);
    }

    private static bool TryParseFloatingPoint(string text, out double number) =>
        double.TryParse(text, NumberSyntax, CultureInfo.InvariantCulture, out number)
        && (double.IsFinite(number) || text is "NaN" or "Infinity" or "-Infinity");

    private static ProtobufJsonError Succeed(object result, out object? value)
    {
        value = result;
        return ProtobufJsonError.None;
    }

    private static ProtobufJsonError TryReadBytes(JsonElement element, out object? value)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            return Fail(ProtobufJsonError.WrongType, out value);
        }

        // Standard or URL-safe base64, padded or not.
        var text = element.GetString()!.Replace('-', '+').Replace('_', '/');
        text = text.PadRight((text.Length + 3) / 4 * 4, '=');
        var buffer = new byte[text.Length / 4 * 3];
        var valid = Convert.TryFromBase64String(text, buffer, out var written);
        value = valid ? buffer[..written] : null;
        return valid ? ProtobufJsonError.None : ProtobufJsonError.InvalidValue;
    }

    private static ProtobufJsonError Fail(ProtobufJsonError error, out object? value)
    {
        value = null;
        return error;
    }

    private sealed record IntegerRange(decimal Minimum, decimal Maximum, Func<decimal, object> Convert)
    {
        public static readonly IntegerRange Int32 = new(int.MinValue, int.MaxValue, number => (int)number);
        public static readonly IntegerRange Int64 = new(long.MinValue, long.MaxValue, number => (long)number);
        public static readonly IntegerRange UInt32 = new(uint.MinValue, uint.MaxValue, number => (uint)number);
        public static readonly IntegerRange UInt64 = new(ulong.MinValue, ulong.MaxValue, number => (ulong)number);
    }
}
