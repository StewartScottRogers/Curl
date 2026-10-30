using Curl.Extension.Grpc.Wire;
using Curl.Protobuf.Descriptors;
using Curl.Protobuf.DynamicMessages;
using Curl.Protobuf.Json;
using static Curl.Extension.Grpc.Reflection.ReflectionFixture;

namespace Curl.Extension.Grpc.Reflection;

/// <summary>Checks the decoding of serialized <c>FileDescriptorProto</c>s (descriptor.proto) into the <c>.proto</c> parser's descriptors.</summary>
[TestClass]
public sealed class FileDescriptorProtoDecoderTests
{
    [TestMethod]
    public void Decode_HelloWorld_GivesItsNameMessagesAndService()
    {
        ReflectedProtoFile file = FileDescriptorProtoDecoder.Decode(HelloWorldFile);

        Assert.AreEqual("helloworld.proto", file.Name);
        Assert.IsEmpty(file.Dependencies);
        Assert.AreEqual(("proto3", "helloworld"), (file.Descriptor.Syntax, file.Descriptor.Package));
        CollectionAssert.AreEqual(new[] { "HelloRequest", "HelloReply" }, file.Descriptor.Messages.Select(static message => message.Name).ToArray());
        ProtoFieldDescriptor name = file.Descriptor.Messages[0].Fields.Single();
        Assert.AreEqual(("name", 1, ProtoFieldLabel.Singular, new ProtoTypeReference(ProtoFieldType.String, "string")), (name.Name, name.Number, name.Label, name.Type));
        Assert.IsTrue(name.MapKey is null && name.MapValue is null && name.OneofName is null && name.Options.Count == 0);
        ProtoMethodDescriptor method = file.Descriptor.Services.Single().Methods.Single();
        Assert.AreEqual(("Greeter", "SayHello", ".helloworld.HelloRequest", ".helloworld.HelloReply", false, false), (file.Descriptor.Services[0].Name, method.Name, method.InputType.Name, method.OutputType.Name, method.ClientStreaming, method.ServerStreaming));
    }

    [TestMethod]
    public void Decode_StreamingMethodAndDependencies_KeepsThem()
    {
        byte[] bytes = File("a.proto", string.Empty, "proto3", ["b.proto", "c.proto"], [], [], [Service("S", [("M", ".B", ".C")], isStreaming: true)]);

        ReflectedProtoFile file = FileDescriptorProtoDecoder.Decode(bytes);

        CollectionAssert.AreEqual(new[] { "b.proto", "c.proto" }, file.Dependencies.ToArray());
        CollectionAssert.AreEqual(new[] { "b.proto", "c.proto" }, file.Descriptor.Imports.Select(static import => import.Path).ToArray());
        Assert.IsTrue(file.Descriptor.Services[0].Methods[0].ClientStreaming && file.Descriptor.Services[0].Methods[0].ServerStreaming);
        Assert.AreEqual(string.Empty, file.Descriptor.Package);
    }

    [TestMethod]
    public void Decode_EveryScalarTypeAndNamedTypes_MapsDescriptorTypeNumbers()
    {
        int[] scalarTypes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 12, 13, 15, 16, 17, 18];
        byte[][] fields = [.. scalarTypes.Select((type, index) => Field($"f{type}", index + 1, 1, type)), Field("m", 20, 1, 11, ".p.M"), Field("e", 21, 1, 14, ".p.E")];

        ProtoMessageDescriptor message = FileDescriptorProtoDecoder.Decode(File("p.proto", "p", "proto3", [], [Message("M", fields)], [Enum("E", ("ZERO", 0), ("MINUS", -1))], [])).Descriptor.Messages[0];

        CollectionAssert.AreEqual(
            new[] { "double", "float", "int64", "uint64", "int32", "fixed64", "fixed32", "bool", "string", "bytes", "uint32", "sfixed32", "sfixed64", "sint32", "sint64", ".p.M", ".p.E" },
            message.Fields.Select(static field => field.Type.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { ProtoFieldType.Double, ProtoFieldType.Float, ProtoFieldType.Int64, ProtoFieldType.UInt64, ProtoFieldType.Int32, ProtoFieldType.Fixed64, ProtoFieldType.Fixed32, ProtoFieldType.Bool, ProtoFieldType.String, ProtoFieldType.Bytes, ProtoFieldType.UInt32, ProtoFieldType.SFixed32, ProtoFieldType.SFixed64, ProtoFieldType.SInt32, ProtoFieldType.SInt64, ProtoFieldType.Named, ProtoFieldType.Named },
            message.Fields.Select(static field => field.Type.Type).ToArray());
    }

    [TestMethod]
    public void Decode_EnumWithNegativeValue_KeepsItsValues()
    {
        ProtoEnumDescriptor enumType = FileDescriptorProtoDecoder.Decode(File("p.proto", "p", "proto3", [], [], [Enum("E", ("ZERO", 0), ("MINUS", -1))], [])).Descriptor.Enums.Single();

        Assert.AreEqual("E", enumType.Name);
        CollectionAssert.AreEqual(new[] { "ZERO=0", "MINUS=-1" }, enumType.Values.Select(static value => $"{value.Name}={value.Number}").ToArray());
    }

    [TestMethod]
    public void Decode_MapField_GivesAMapWithKeyAndValueAndDropsTheEntryMessage()
    {
        byte[] entry = Message("CountsEntry", [Field("key", 1, 1, 9), Field("value", 2, 1, 11, ".p.M.Inner")], isMapEntry: true);
        byte[] message = Message("M", [Field("counts", 1, 3, 11, ".p.M.CountsEntry")], [entry, Message("Inner", [])]);

        ProtoMessageDescriptor decoded = FileDescriptorProtoDecoder.Decode(File("p.proto", "p", "proto3", [], [message], [], [])).Descriptor.Messages[0];

        ProtoFieldDescriptor counts = decoded.Fields.Single();
        Assert.AreEqual((ProtoFieldType.Map, ProtoFieldLabel.Repeated), (counts.Type.Type, counts.Label));
        Assert.AreEqual(("string", ".p.M.Inner"), (counts.MapKey!.Name, counts.MapValue!.Name));
        CollectionAssert.AreEqual(new[] { "Inner" }, decoded.Messages.Select(static nested => nested.Name).ToArray());
    }

    [TestMethod]
    public void Decode_MapEntryWithoutValue_ThrowsGrpcWireException()
    {
        byte[] entry = Message("CountsEntry", [Field("key", 1, 1, 9)], isMapEntry: true);
        byte[] message = Message("M", [Field("counts", 1, 3, 11, ".M.CountsEntry")], [entry]);

        GrpcWireException failure = Assert.ThrowsExactly<GrpcWireException>(() => FileDescriptorProtoDecoder.Decode(File("p.proto", string.Empty, "proto3", [], [message], [], [])));

        Assert.AreEqual("The map entry CountsEntry has no field 2.", failure.Message);
    }

    [TestMethod]
    public void Decode_OneofAndProto3Optional_GivesTheRealOneofOnly()
    {
        byte[] message = Message(
            "M",
            [Field("a", 1, 1, 9, oneofIndex: 0), Field("b", 2, 1, 5, oneofIndex: 1, isProto3Optional: true), Field("c", 3, 1, 5), Field("d", 4, 1, 5, oneofIndex: 7)],
            oneofs: ["choice", "_b"]);

        ProtoMessageDescriptor decoded = FileDescriptorProtoDecoder.Decode(File("p.proto", "p", "proto3", [], [message], [], [])).Descriptor.Messages[0];

        CollectionAssert.AreEqual(
            new[] { "a Singular choice", "b Optional ", "c Singular ", "d Singular " },
            decoded.Fields.Select(static field => $"{field.Name} {field.Label} {field.OneofName}").ToArray());
        CollectionAssert.AreEqual(new[] { "choice", "_b" }, decoded.Oneofs.Select(static oneof => oneof.Name).ToArray());
    }

    [TestMethod]
    [DataRow("proto2", null, true)]
    [DataRow("proto2", true, false)]
    [DataRow("proto3", null, false)]
    [DataRow("proto3", false, true)]
    [DataRow(null, null, true)]
    public void Decode_RepeatedScalar_IsPackedBySyntaxUnlessItsOptionSays(string? syntax, bool? packed, bool isUnpacked)
    {
        byte[] message = Message("M", [Field("values", 1, 3, 5, packed: packed)]);

        ProtoFieldDescriptor field = FileDescriptorProtoDecoder.Decode(File("p.proto", "p", syntax, [], [message], [], [])).Descriptor.Messages[0].Fields[0];

        Assert.AreEqual(ProtoFieldLabel.Repeated, field.Label);
        Assert.AreEqual(isUnpacked, field.Options.Any(static option => option is { Name: "packed", Value: "false" }));
    }

    [TestMethod]
    public void Decode_FileWithoutSyntax_IsProto2WithPresence()
    {
        ReflectedProtoFile file = FileDescriptorProtoDecoder.Decode(File("p.proto", "p", null, [], [Message("M", [Field("x", 1, 2, 5)])], [], []));

        Assert.AreEqual("proto2", file.Descriptor.Syntax);
        Assert.AreEqual(ProtoFieldLabel.Optional, file.Descriptor.Messages[0].Fields[0].Label);
    }

    [TestMethod]
    public void Decode_NestedMessagesAndEnums_QualifiesMapEntriesByTheirScope()
    {
        byte[] entry = Message("TagsEntry", [Field("key", 1, 1, 9), Field("value", 2, 1, 14, ".p.Outer.Inner.Kind")], isMapEntry: true);
        byte[] inner = Message("Inner", [Field("tags", 1, 3, 11, ".p.Outer.Inner.TagsEntry")], [entry], [Enum("Kind", ("A", 0))]);
        byte[] outer = Message("Outer", [Field("inner", 1, 1, 11, ".p.Outer.Inner")], [inner]);
        ReflectedProtoFile file = FileDescriptorProtoDecoder.Decode(File("p.proto", "p", "proto3", [], [outer], [], []));
        var catalogue = new DynamicTypeCatalogue([file.Descriptor]);

        ProtobufJsonError error = ProtobufJsonConverter.TryConvertJsonToBinary(catalogue.GetMessageType("p.Outer"), "{\"inner\":{\"tags\":{\"x\":\"A\"}}}"u8.ToArray(), out byte[]? binary);

        Assert.AreEqual(ProtobufJsonError.None, error);
        Assert.IsNotNull(binary);
        Assert.AreEqual(ProtoFieldType.Map, file.Descriptor.Messages[0].Messages[0].Fields[0].Type.Type);
    }

    [TestMethod]
    public void Decode_GroupField_ThrowsGrpcWireException()
    {
        GrpcWireException failure = Assert.ThrowsExactly<GrpcWireException>(() => FileDescriptorProtoDecoder.Decode(File("p.proto", "p", "proto2", [], [Message("M", [Field("g", 1, 1, 10, ".p.G")])], [], [])));

        Assert.AreEqual("The field g has type 10, which a gRPC call cannot carry.", failure.Message);
    }

    [TestMethod]
    public void Decode_NotAMessage_ThrowsGrpcWireException() =>
        Assert.ThrowsExactly<GrpcWireException>(() => FileDescriptorProtoDecoder.Decode(new byte[] { 0x0A, 0x05, 0x61 }));
}
