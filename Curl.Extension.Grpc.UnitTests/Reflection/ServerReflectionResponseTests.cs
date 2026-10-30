using Curl.Extension.Grpc.Wire;
using static Curl.Extension.Grpc.Reflection.ReflectionFixture;

namespace Curl.Extension.Grpc.Reflection;

/// <summary>Checks the decoding of <c>grpc.reflection.v1.ServerReflectionResponse</c> (reflection.proto).</summary>
[TestClass]
public sealed class ServerReflectionResponseTests
{
    [TestMethod]
    public void Decode_FileDescriptorResponse_GivesEveryFileAndNoError()
    {
        ServerReflectionResponse response = ServerReflectionResponse.Decode(FilesResponse(HelloWorldFile, [0x0A, 0x01, 0x61]));

        Assert.HasCount(2, response.FileDescriptorProtos);
        CollectionAssert.AreEqual(HelloWorldFile, response.FileDescriptorProtos[0].ToArray());
        Assert.IsNull(response.Error);
    }

    [TestMethod]
    public void Decode_ErrorResponse_GivesItsCodeAndMessage()
    {
        ServerReflectionResponse response = ServerReflectionResponse.Decode(ErrorResponse(5, "symbol not found: x.Y"));

        Assert.AreEqual((5, "symbol not found: x.Y"), response.Error);
        Assert.IsEmpty(response.FileDescriptorProtos);
    }

    [TestMethod]
    public void Decode_EmptyMessage_GivesNoFilesAndNoError()
    {
        ServerReflectionResponse response = ServerReflectionResponse.Decode(ReadOnlyMemory<byte>.Empty);

        Assert.IsEmpty(response.FileDescriptorProtos);
        Assert.IsNull(response.Error);
    }

    [TestMethod]
    public void Decode_NotAMessage_ThrowsGrpcWireException()
    {
        GrpcWireException failure = Assert.ThrowsExactly<GrpcWireException>(() => ServerReflectionResponse.Decode(new byte[] { 0x22, 0x09 }));

        Assert.AreEqual("A server reflection message does not decode (LengthPastEnd).", failure.Message);
    }
}
