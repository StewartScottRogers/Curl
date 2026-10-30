namespace Curl.Extension.Grpc.Wire;

/// <summary>Checks the Request-Headers of PROTOCOL-HTTP2.md "Requests".</summary>
[TestClass]
public sealed class GrpcRequestHeadersTests
{
    private static readonly GrpcRequestTarget Greeter = new("https", "example.com:443", "helloworld.Greeter", "SayHello");

    [TestMethod]
    public void Build_Requests_GivesTheCallDefinitionInWireOrder()
    {
        var headers = GrpcRequestHeaders.Build(Greeter, TimeSpan.FromSeconds(1.5), "curl/8.17.0");

        CollectionAssert.AreEqual(
            new KeyValuePair<string, string>[]
            {
                new(":method", "POST"),
                new(":scheme", "https"),
                new(":path", "/helloworld.Greeter/SayHello"),
                new(":authority", "example.com:443"),
                new("te", "trailers"),
                new("grpc-timeout", "1500000u"),
                new("content-type", "application/grpc"),
                new("user-agent", "curl/8.17.0"),
            },
            headers.ToArray());
    }

    [TestMethod]
    public void Build_Requests_NoTimeoutOrUserAgent_LeavesThemOut()
    {
        var headers = GrpcRequestHeaders.Build(Greeter with { Scheme = "http" }, null, null);

        CollectionAssert.AreEqual(
            new[] { ":method", ":scheme", ":path", ":authority", "te", "content-type" },
            headers.Select(header => header.Key).ToArray());
        Assert.AreEqual("http", headers[1].Value);
    }

    [TestMethod]
    [DataRow("ftp", "example.com", "helloworld.Greeter", "SayHello")]
    [DataRow("https", "", "helloworld.Greeter", "SayHello")]
    [DataRow("https", "example.com", "", "SayHello")]
    [DataRow("https", "example.com", "a/b", "SayHello")]
    [DataRow("https", "example.com", "helloworld.Greeter", "")]
    [DataRow("https", "example.com", "helloworld.Greeter", "Say/Hello")]
    public void Build_Requests_InvalidTarget_IsRefused(string scheme, string authority, string service, string method) =>
        Assert.ThrowsExactly<ArgumentException>(() => GrpcRequestHeaders.Build(new GrpcRequestTarget(scheme, authority, service, method), null, null));

    [TestMethod]
    public void Build_Requests_NullTarget_IsRefused() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => GrpcRequestHeaders.Build(null!, null, null));
}
