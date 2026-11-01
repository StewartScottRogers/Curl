using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Grpc.Client;

/// <summary>Checks that a URL path is read as PROTOCOL-HTTP2.md's <c>:path</c>, <c>/Service-Name/method</c>.</summary>
[TestClass]
public sealed class GrpcMethodPathTests
{
    [TestMethod]
    public void Parse_ServiceAndMethod_GivesBoth() =>
        Assert.AreEqual(new GrpcMethodPath("helloworld.Greeter", "SayHello"), GrpcMethodPath.Parse("/helloworld.Greeter/SayHello"));

    [TestMethod]
    [DataRow("")]
    [DataRow("/")]
    [DataRow("/helloworld.Greeter")]
    [DataRow("/helloworld.Greeter/")]
    [DataRow("//SayHello")]
    [DataRow("/a/b/c")]
    public void Parse_NotServiceAndMethod_ThrowsWithExit3(string path)
    {
        GrpcClientException failure = Assert.ThrowsExactly<GrpcClientException>(() => GrpcMethodPath.Parse(path));

        Assert.AreEqual(CurlExitCode.UrlMalformat, failure.ExitCode);
        Assert.AreEqual($"A grpc:// URL names the method as /package.Service/Method, not \"{path}\"", failure.Message);
    }
}
