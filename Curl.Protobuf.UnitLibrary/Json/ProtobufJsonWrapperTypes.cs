using System.Text.Json;
using Curl.Protobuf.DynamicMessages;

namespace Curl.Protobuf.Json;

/// <summary>
/// The JSON forms of the nine wrapper types (<c>google.protobuf.DoubleValue</c> to
/// <c>google.protobuf.BytesValue</c>): the wrapped <c>value</c> field's own JSON value, so a
/// present wrapper holding zero is written as that zero rather than left out.
/// </summary>
internal static class ProtobufJsonWrapperTypes
{
    private static readonly HashSet<string> WrapperNames =
    [
        "google.protobuf.DoubleValue", "google.protobuf.FloatValue", "google.protobuf.Int64Value",
        "google.protobuf.UInt64Value", "google.protobuf.Int32Value", "google.protobuf.UInt32Value",
        "google.protobuf.BoolValue", "google.protobuf.StringValue", "google.protobuf.BytesValue",
    ];

    /// <summary>Whether messages of this type are a wrapper type.</summary>
    public static bool IsWrapper(DynamicMessageType type) => WrapperNames.Contains(type.FullName);

    /// <summary>Writes the wrapped value, or its default when the wrapper holds none.</summary>
    public static void Write(Utf8JsonWriter writer, DynamicMessage message)
    {
        var field = message.Type.FindField(1)!;
        ProtobufJsonScalars.Write(writer, message.FindValue(1) ?? field.CreateDefaultValue());
    }

    /// <summary>Reads the wrapped value into the wrapper message.</summary>
    public static ProtobufJsonError Read(JsonElement element, DynamicMessage message)
    {
        var field = message.Type.FindField(1)!;
        var error = ProtobufJsonScalars.Read(element, field.Kind, out var value);
        if (error == ProtobufJsonError.None)
        {
            message.SetSingular(field, value!);
        }

        return error;
    }
}
