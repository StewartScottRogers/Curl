using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Grpc.Client;

/// <summary>Checks that the method's message types are read from the <c>--grpc-proto</c> file and the files it imports.</summary>
[TestClass]
public sealed class GrpcProtoMethodTests
{
    [TestMethod]
    public void Load_MethodOfTheFile_GivesItsInputAndImportedOutputTypes()
    {
        using var proto = new ProtoFixture();

        GrpcProtoMethod method = GrpcProtoMethod.Load(proto.GreeterPath, new GrpcMethodPath("helloworld.Greeter", "SayHello"));

        Assert.AreEqual("helloworld.HelloRequest", method.Input.FullName);
        Assert.AreEqual("helloworld.HelloReply", method.Output.FullName);
    }

    [TestMethod]
    public void Load_FileWithoutPackage_FindsTheServiceByItsBareName()
    {
        using var proto = new ProtoFixture();
        string path = proto.Write("bare.proto", "syntax = \"proto3\";\nservice Echo {\n  rpc Say (Text) returns (Text);\n}\nmessage Text {\n  string text = 1;\n}\n");

        GrpcProtoMethod method = GrpcProtoMethod.Load(path, new GrpcMethodPath("Echo", "Say"));

        Assert.AreEqual("Text", method.Input.FullName);
    }

    [TestMethod]
    public void Load_MissingFile_ThrowsWithExit26()
    {
        using var proto = new ProtoFixture();

        GrpcClientException failure = Assert.ThrowsExactly<GrpcClientException>(() => GrpcProtoMethod.Load(Path.Combine(proto.Directory, "none.proto"), new GrpcMethodPath("a.B", "C")));

        Assert.AreEqual(CurlExitCode.ReadError, failure.ExitCode);
        StringAssert.StartsWith(failure.Message, "--grpc-proto: none.proto:");
    }

    [TestMethod]
    public void Load_UnknownService_ThrowsWithExit2()
    {
        using var proto = new ProtoFixture();

        GrpcClientException failure = Assert.ThrowsExactly<GrpcClientException>(() => GrpcProtoMethod.Load(proto.GreeterPath, new GrpcMethodPath("helloworld.Other", "SayHello")));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual($"--grpc-proto: {proto.GreeterPath} declares no service helloworld.Other", failure.Message);
    }

    [TestMethod]
    public void Load_UnknownMethod_ThrowsWithExit2()
    {
        using var proto = new ProtoFixture();

        GrpcClientException failure = Assert.ThrowsExactly<GrpcClientException>(() => GrpcProtoMethod.Load(proto.GreeterPath, new GrpcMethodPath("helloworld.Greeter", "SayBye")));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual("--grpc-proto: service helloworld.Greeter has no method SayBye", failure.Message);
    }
}
