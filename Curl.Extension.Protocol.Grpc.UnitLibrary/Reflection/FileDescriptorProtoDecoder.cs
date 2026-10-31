using Curl.Extension.Protocol.Grpc.Wire;
using Curl.Protobuf.Descriptors;

namespace Curl.Extension.Protocol.Grpc.Reflection;

/// <summary>
/// Decodes a serialized <c>google.protobuf.FileDescriptorProto</c> (descriptor.proto) into the descriptors the <c>.proto</c>
/// parser makes (ADR-1149): messages with their fields, oneofs, nested types and map entries, enums and services. Type names
/// stay fully qualified with a leading dot, which <c>DynamicTypeCatalogue</c> resolves as they are.
/// </summary>
internal static class FileDescriptorProtoDecoder
{
    private const int LabelRepeated = 3;

    /// <summary>descriptor.proto's <c>FieldDescriptorProto.Type</c> numbers, each with the type and keyword it stands for.</summary>
    private static readonly Dictionary<long, (ProtoFieldType Type, string Keyword)> FieldTypes = new()
    {
        [1] = (ProtoFieldType.Double, "double"),
        [2] = (ProtoFieldType.Float, "float"),
        [3] = (ProtoFieldType.Int64, "int64"),
        [4] = (ProtoFieldType.UInt64, "uint64"),
        [5] = (ProtoFieldType.Int32, "int32"),
        [6] = (ProtoFieldType.Fixed64, "fixed64"),
        [7] = (ProtoFieldType.Fixed32, "fixed32"),
        [8] = (ProtoFieldType.Bool, "bool"),
        [9] = (ProtoFieldType.String, "string"),
        [11] = (ProtoFieldType.Named, string.Empty),
        [12] = (ProtoFieldType.Bytes, "bytes"),
        [13] = (ProtoFieldType.UInt32, "uint32"),
        [14] = (ProtoFieldType.Named, string.Empty),
        [15] = (ProtoFieldType.SFixed32, "sfixed32"),
        [16] = (ProtoFieldType.SFixed64, "sfixed64"),
        [17] = (ProtoFieldType.SInt32, "sint32"),
        [18] = (ProtoFieldType.SInt64, "sint64"),
    };

    /// <summary>Decodes the file.</summary>
    /// <param name="serialized">The <c>FileDescriptorProto</c>'s binary form.</param>
    /// <returns>The file's name, dependencies and descriptor.</returns>
    /// <exception cref="GrpcWireException">The bytes do not decode, or a field has a type gRPC calls cannot carry (a group).</exception>
    public static ReflectedProtoFile Decode(ReadOnlyMemory<byte> serialized)
    {
        ReflectionMessageFields file = ReflectionMessageFields.Read(serialized);
        string syntax = file.Has(12) ? file.String(12) : "proto2";
        string package = file.String(2);
        List<string> dependencies = [.. file.Strings(3)];
        var descriptor = new ProtoFileDescriptor(
            syntax,
            package,
            [.. dependencies.Select(static path => new ProtoImportDescriptor(path, ProtoImportKind.Default, 0, 0))],
            [],
            [.. file.Messages(4).Select(message => DecodeMessage(message, package, syntax))],
            [.. file.Messages(5).Select(DecodeEnum)],
            [.. file.Messages(6).Select(DecodeService)]);
        return new ReflectedProtoFile(file.String(1), dependencies, descriptor);
    }

    private static string Qualify(string scope, string name) => scope.Length == 0 ? name : $"{scope}.{name}";

    /// <summary>Tells whether a nested message is a map field's entry (<c>MessageOptions.map_entry</c>, field 7).</summary>
    private static bool IsMapEntry(ReflectionMessageFields message) => (message.Message(7)?.Integer(7) ?? 0) != 0;

    private static ProtoMessageDescriptor DecodeMessage(ReflectionMessageFields message, string scope, string syntax)
    {
        string name = message.String(1);
        string fullName = Qualify(scope, name);
        List<ReflectionMessageFields> nested = [.. message.Messages(3)];
        Dictionary<string, ReflectionMessageFields> mapEntries = nested.Where(IsMapEntry)
            .ToDictionary(entry => $".{Qualify(fullName, entry.String(1))}", StringComparer.Ordinal);
        List<string> oneofs = [.. message.Messages(8).Select(static oneof => oneof.String(1))];
        List<ProtoFieldDescriptor> fields = [.. message.Messages(2).Select(field => DecodeField(field, syntax, oneofs, mapEntries))];
        List<ProtoOneofDescriptor> oneofDescriptors = [.. oneofs.Select(static oneof => new ProtoOneofDescriptor(oneof, []))];
        List<ProtoMessageDescriptor> messages = [.. nested.Where(static entry => !IsMapEntry(entry)).Select(entry => DecodeMessage(entry, fullName, syntax))];
        List<ProtoEnumDescriptor> enums = [.. message.Messages(4).Select(static enumType => DecodeEnum(enumType))];
        return new ProtoMessageDescriptor(name, fields, oneofDescriptors, messages, enums, [], [], []);
    }

    private static ProtoFieldDescriptor DecodeField(
        ReflectionMessageFields field, string syntax, List<string> oneofs, Dictionary<string, ReflectionMessageFields> mapEntries)
    {
        string name = field.String(1);
        int number = (int)field.Integer(3);
        if (mapEntries.TryGetValue(field.String(6), out ReflectionMessageFields? entry))
        {
            return new ProtoFieldDescriptor(
                name, number, ProtoFieldLabel.Repeated, new ProtoTypeReference(ProtoFieldType.Map, string.Empty),
                TypeOf(MapEntryField(entry, 1)), TypeOf(MapEntryField(entry, 2)), null, []);
        }

        bool isProto3Optional = field.Integer(17) != 0;
        string? oneofName = field.Has(9) && !isProto3Optional ? oneofs.ElementAtOrDefault((int)field.Integer(9)) : null;
        return new ProtoFieldDescriptor(name, number, LabelOf(field, syntax, isProto3Optional, oneofName), TypeOf(field), null, null, oneofName, PackingOf(field, syntax));
    }

    /// <summary>Gives a map entry's key (field 1) or value (field 2).</summary>
    private static ReflectionMessageFields MapEntryField(ReflectionMessageFields entry, int number) =>
        entry.Messages(2).FirstOrDefault(field => field.Integer(3) == number)
        ?? throw new GrpcWireException($"The map entry {entry.String(1)} has no field {number}.");

    /// <summary>
    /// A repeated field is repeated; a field in a real oneof is singular; otherwise a field has presence (optional) when proto3
    /// marks it <c>optional</c> or its file is not proto3, as in proto2 and editions.
    /// </summary>
    private static ProtoFieldLabel LabelOf(ReflectionMessageFields field, string syntax, bool isProto3Optional, string? oneofName) =>
        field.Integer(4) == LabelRepeated ? ProtoFieldLabel.Repeated
        : oneofName is null && (isProto3Optional || syntax != "proto3") ? ProtoFieldLabel.Optional
        : ProtoFieldLabel.Singular;

    /// <summary>
    /// <c>[packed = false]</c> when the field is not packed: <c>FieldOptions.packed</c> (field 2) when set, otherwise packed
    /// except in proto2.
    /// </summary>
    private static List<ProtoOption> PackingOf(ReflectionMessageFields field, string syntax)
    {
        ReflectionMessageFields? options = field.Message(8);
        bool isPacked = options is not null && options.Has(2) ? options.Integer(2) != 0 : syntax != "proto2";
        return isPacked ? [] : [new ProtoOption("packed", "false")];
    }

    private static ProtoTypeReference TypeOf(ReflectionMessageFields field)
    {
        long type = field.Integer(5);
        return FieldTypes.TryGetValue(type, out (ProtoFieldType Type, string Keyword) known)
            ? new ProtoTypeReference(known.Type, known.Type == ProtoFieldType.Named ? field.String(6) : known.Keyword)
            : throw new GrpcWireException($"The field {field.String(1)} has type {type}, which a gRPC call cannot carry.");
    }

    private static ProtoEnumDescriptor DecodeEnum(ReflectionMessageFields enumType) =>
        new(
            enumType.String(1),
            [.. enumType.Messages(2).Select(static value => new ProtoEnumValueDescriptor(value.String(1), (int)value.Integer(2), []))],
            [],
            [],
            []);

    private static ProtoServiceDescriptor DecodeService(ReflectionMessageFields service) =>
        new(service.String(1), [.. service.Messages(2).Select(DecodeMethod)], []);

    private static ProtoMethodDescriptor DecodeMethod(ReflectionMessageFields method) =>
        new(
            method.String(1),
            new ProtoTypeReference(ProtoFieldType.Named, method.String(2)),
            method.Integer(5) != 0,
            new ProtoTypeReference(ProtoFieldType.Named, method.String(3)),
            method.Integer(6) != 0,
            []);
}
