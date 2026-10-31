namespace Curl.Extension.Grpc.Reflection;

/// <summary>Checks the bytes of <c>grpc.reflection.v1.ServerReflectionRequest</c> messages (reflection.proto).</summary>
[TestClass]
public sealed class ServerReflectionRequestTests
{
    [TestMethod]
    public void FileContainingSymbol_WritesField4() =>
        CollectionAssert.AreEqual((byte[])[0x22, 0x12, .. "helloworld.Greeter"u8], ServerReflectionRequest.FileContainingSymbol("helloworld.Greeter"));

    [TestMethod]
    public void FileByFilename_WritesField3() =>
        CollectionAssert.AreEqual((byte[])[0x1A, 0x07, .. "a.proto"u8], ServerReflectionRequest.FileByFilename("a.proto"));
}
