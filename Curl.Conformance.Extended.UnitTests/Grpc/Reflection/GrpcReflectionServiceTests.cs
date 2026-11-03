using Curl.Conformance.Extended.Grpc;
using Curl.Conformance.Extended.Grpc.Reflection;
using Curl.Http2;

namespace Curl.Conformance.Extended.UnitTests.Grpc.Reflection;

/// <summary>The reference reflection service, its request reader, message writer and companion services, apart from curl.</summary>
[TestClass]
public sealed class GrpcReflectionServiceTests
{
    private static readonly GrpcReflectedFile Root = new("a.proto", [1], ["p.A"], ["b.proto"]);

    [TestMethod]
    public void Answer_OtherPathOrNoMessageYet_ReturnsNull()
    {
        var service = new GrpcReflectionService([Root]);

        Assert.IsNull(service.Answer(Call("/p.A/M", [])));
        Assert.IsNull(service.Answer(Call(GrpcReflectionService.Path)));
    }

    [TestMethod]
    public void Answer_FileByFilenameFirst_AnswersThatFile()
    {
        var service = new GrpcReflectionService([Root with { Dependencies = [] }]);

        GrpcReferenceReply? reply = service.Answer(Call(GrpcReflectionService.Path, GrpcReflectionRequest.FileByFilename("a.proto")));

        Assert.IsNotNull(reply);
        Assert.HasCount(1, reply.DataFrames);
    }

    [TestMethod]
    public void Answer_RequestKindItDoesNotServe_AnswersAnErrorResponse()
    {
        var service = new GrpcReflectionService([Root]);
        byte[] listServices = new ProtobufMessageWriter().String(7, "*").ToArray();

        GrpcReferenceReply? reply = service.Answer(Call(GrpcReflectionService.Path, listServices));

        byte[] expected = new ProtobufMessageWriter()
            .Bytes(2, listServices)
            .Message(7, new ProtobufMessageWriter().Varint(1, GrpcReflectionService.NotFoundStatus).String(2, "* not found"))
            .ToArray();
        Assert.IsNotNull(reply);
        CollectionAssert.AreEqual(GrpcLengthPrefixedMessage.Frame(expected), reply.DataFrames.Single());
    }

    [TestMethod]
    public void Answer_DependencyItWasNotGiven_Throws()
    {
        var service = new GrpcReflectionService([Root]);
        byte[] request = new ProtobufMessageWriter().String(GrpcReflectionRequest.FileContainingSymbolField, "p.A").ToArray();

        Assert.ThrowsExactly<InvalidOperationException>(() => service.Answer(Call(GrpcReflectionService.Path, request)));
    }

    [TestMethod]
    public void Answer_NullCall_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new GrpcReflectionService([]).Answer(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new GrpcEchoService("/x/y").Answer(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new GrpcUnimplementedMethodService("/x/y").Answer(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ProtobufMessageWriter().Message(1, null!));
    }

    [TestMethod]
    public void EchoAndUnimplemented_CallTheyDoNotServe_ReturnNull()
    {
        GrpcReferenceCall open = Call("/x/y", [1]);

        Assert.IsNull(new GrpcEchoService("/x/y").Answer(open));
        Assert.IsNull(new GrpcEchoService("/x/z").Answer(Call("/x/y", [1], isEnded: true)));
        Assert.IsNull(new GrpcUnimplementedMethodService("/x/z").Answer(open));
    }

    [TestMethod]
    public void Parse_HostAndLongValue_ReadsBoth()
    {
        string symbol = new('s', 200);
        byte[] request = new ProtobufMessageWriter().String(GrpcReflectionRequest.HostField, "h").String(GrpcReflectionRequest.FileContainingSymbolField, symbol).ToArray();

        GrpcReflectionRequest parsed = GrpcReflectionRequest.Parse(request);

        Assert.AreEqual(new GrpcReflectionRequest("h", GrpcReflectionRequest.FileContainingSymbolField, symbol), parsed);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x20, 0x01 })]
    [DataRow(new byte[] { 0x22, 0x05, 0x61 })]
    [DataRow(new byte[] { 0x22, 0x80 })]
    public void Parse_MalformedField_Throws(byte[] request) =>
        Assert.ThrowsExactly<InvalidDataException>(() => GrpcReflectionRequest.Parse(request));

    [TestMethod]
    public void Fixed_Fields_AreWrittenLittleEndian()
    {
        byte[] written = new ProtobufMessageWriter().Fixed32(1, 0x01020304).Fixed64(2, 0x0102030405060708).ToArray();

        CollectionAssert.AreEqual(new byte[] { 0x0D, 4, 3, 2, 1, 0x11, 8, 7, 6, 5, 4, 3, 2, 1 }, written);
    }

    private static GrpcReferenceCall Call(string path, byte[]? message = null, bool isEnded = false)
    {
        var call = new GrpcReferenceCall(1, [new HeaderField(":path", path)]);
        if (message is not null)
        {
            call.AddData(GrpcLengthPrefixedMessage.Frame(message));
        }

        if (isEnded)
        {
            call.EndRequest();
        }

        return call;
    }
}
