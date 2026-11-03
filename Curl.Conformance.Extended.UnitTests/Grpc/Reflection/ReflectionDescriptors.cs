using Curl.Conformance.Extended.Grpc.Reflection;

namespace Curl.Conformance.Extended.UnitTests.Grpc.Reflection;

/// <summary>
/// Serialized <c>google.protobuf.FileDescriptorProto</c>s (descriptor.proto) written by hand for the reflection cases, and
/// the one curl run they share.
/// </summary>
internal static class ReflectionDescriptors
{
    internal const int TypeDouble = 1;
    internal const int TypeFloat = 2;
    internal const int TypeInt64 = 3;
    internal const int TypeUInt64 = 4;
    internal const int TypeInt32 = 5;
    internal const int TypeFixed64 = 6;
    internal const int TypeFixed32 = 7;
    internal const int TypeBool = 8;
    internal const int TypeString = 9;
    internal const int TypeMessage = 11;
    internal const int TypeBytes = 12;
    internal const int TypeUInt32 = 13;
    internal const int TypeEnum = 14;
    internal const int TypeSFixed32 = 15;
    internal const int TypeSFixed64 = 16;
    internal const int TypeSInt32 = 17;
    internal const int TypeSInt64 = 18;

    private const int LabelOptional = 1;
    private const int LabelRepeated = 3;

    /// <summary>A <c>FileDescriptorProto</c> in proto3.</summary>
    internal static byte[] File(string name, string package, IReadOnlyList<string> dependencies, IReadOnlyList<ProtobufMessageWriter> messages, IReadOnlyList<ProtobufMessageWriter> enums, IReadOnlyList<ProtobufMessageWriter> services)
    {
        var file = new ProtobufMessageWriter().String(1, name).String(2, package);
        foreach (string dependency in dependencies)
        {
            file.String(3, dependency);
        }

        Add(file, 4, messages);
        Add(file, 5, enums);
        Add(file, 6, services);
        return file.String(12, "proto3").ToArray();
    }

    /// <summary>A <c>DescriptorProto</c>; a map entry when <paramref name="isMapEntry" />.</summary>
    internal static ProtobufMessageWriter Message(string name, IReadOnlyList<ProtobufMessageWriter> fields, IReadOnlyList<ProtobufMessageWriter>? nested = null, bool isMapEntry = false)
    {
        var message = new ProtobufMessageWriter().String(1, name);
        Add(message, 2, fields);
        Add(message, 3, nested ?? []);
        return isMapEntry ? message.Message(7, new ProtobufMessageWriter().Varint(7, 1)) : message;
    }

    /// <summary>A <c>FieldDescriptorProto</c>; <paramref name="typeName" /> is fully qualified with a leading dot.</summary>
    internal static ProtobufMessageWriter Field(string name, int number, int type, string? typeName = null, bool isRepeated = false)
    {
        var field = new ProtobufMessageWriter().String(1, name).Varint(3, number).Varint(4, isRepeated ? LabelRepeated : LabelOptional).Varint(5, type);
        return typeName is null ? field : field.String(6, typeName);
    }

    /// <summary>An <c>EnumDescriptorProto</c> whose values are numbered from 0 in order.</summary>
    internal static ProtobufMessageWriter Enum(string name, params IReadOnlyList<string> values)
    {
        var enumType = new ProtobufMessageWriter().String(1, name);
        for (int number = 0; number < values.Count; number++)
        {
            enumType.Message(2, new ProtobufMessageWriter().String(1, values[number]).Varint(2, number));
        }

        return enumType;
    }

    /// <summary>A <c>ServiceDescriptorProto</c> with one unary method.</summary>
    internal static ProtobufMessageWriter Service(string name, string method, string inputType, string outputType) =>
        new ProtobufMessageWriter().String(1, name).Message(2, new ProtobufMessageWriter().String(1, method).String(2, inputType).String(3, outputType));

    private static void Add(ProtobufMessageWriter parent, int fieldNumber, IReadOnlyList<ProtobufMessageWriter> children)
    {
        foreach (ProtobufMessageWriter child in children)
        {
            parent.Message(fieldNumber, child);
        }
    }
}
