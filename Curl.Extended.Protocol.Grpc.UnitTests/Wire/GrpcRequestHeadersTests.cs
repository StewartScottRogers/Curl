using Curl.Extended.Protocol.Grpc.Client;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Grpc.Wire;

/// <summary>Checks the Request-Headers of PROTOCOL-HTTP2.md "Requests" (ADR-1226, ADR-1227).</summary>
[TestClass]
public sealed class GrpcRequestHeadersTests
{
    private static readonly GrpcRequestTarget Greeter = new("https", "example.com:443", "helloworld.Greeter", "SayHello");

    [TestMethod]
    public void Build_Requests_GivesTheTimeoutStraightAfterThePseudoHeadersAndMetadataLast()
    {
        var headers = GrpcRequestHeaders.Build(Greeter, new GrpcRequestHeaderValues(TimeSpan.FromSeconds(1.5), "curl/8.17.0", ["x-trace: abc"]));

        CollectionAssert.AreEqual(
            new KeyValuePair<string, string>[]
            {
                new(":method", "POST"),
                new(":scheme", "https"),
                new(":path", "/helloworld.Greeter/SayHello"),
                new(":authority", "example.com:443"),
                new("grpc-timeout", "1500000u"),
                new("te", "trailers"),
                new("content-type", "application/grpc"),
                new("user-agent", "curl/8.17.0"),
                new("x-trace", "abc"),
            },
            headers.ToArray());
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Build_Requests_NoTimeoutOrUserAgent_LeavesThemOut(string? userAgent)
    {
        var headers = GrpcRequestHeaders.Build(Greeter with { Scheme = "http" }, new GrpcRequestHeaderValues(null, userAgent, []));

        CollectionAssert.AreEqual(
            new[] { ":method", ":scheme", ":path", ":authority", "te", "content-type" },
            headers.Select(header => header.Key).ToArray());
        Assert.AreEqual("http", headers[1].Value);
    }

    [TestMethod]
    [DataRow("X-Trace:  abc ", "x-trace: abc")]
    [DataRow(" X-Empty ;", "x-empty: ")]
    [DataRow("x-dup: 1|x-dup: 2", "x-dup: 1|x-dup: 2")]
    [DataRow("x-gone:", "")]
    [DataRow("x-gone:   ", "")]
    [DataRow("no separator", "")]
    [DataRow("x-a;b", "")]
    [DataRow(": no name", "")]
    [DataRow(" ;", "")]
    [DataRow(";", "")]
    [DataRow("", "")]
    [DataRow("trace-bin: AQI=", "trace-bin: AQI")]
    [DataRow("trace-bin: AQL/", "trace-bin: AQL/")]
    [DataRow("Trace-Bin: AQ", "trace-bin: AQ")]
    public void Build_CustomHeaders_SendsTheMetadataCurlReadsFromH(string entries, string expected)
    {
        var headers = GrpcRequestHeaders.Build(Greeter, new GrpcRequestHeaderValues(null, null, entries.Split('|')));

        Assert.AreEqual(expected, string.Join("|", headers.Skip(6).Select(static header => $"{header.Key}: {header.Value}")));
    }

    [TestMethod]
    public void Build_CustomHeaderNamingACallDefinitionHeader_ReplacesItInPlace()
    {
        var headers = GrpcRequestHeaders.Build(Greeter, new GrpcRequestHeaderValues(null, "curl/8.22.0", ["User-Agent: probe/1", "content-type: application/grpc+proto"]));

        CollectionAssert.AreEqual(
            new[] { "te: trailers", "content-type: application/grpc+proto", "user-agent: probe/1" },
            headers.Skip(4).Select(static header => $"{header.Key}: {header.Value}").ToArray());
    }

    [TestMethod]
    public void Build_CustomHeaderRemovingACallDefinitionHeader_LeavesItOut()
    {
        var headers = GrpcRequestHeaders.Build(Greeter, new GrpcRequestHeaderValues(null, "curl/8.22.0", ["user-agent:"]));

        Assert.IsFalse(headers.Any(static header => header.Key == "user-agent"));
    }

    [TestMethod]
    public void Build_CustomHeaderNamingAPseudoHeader_IsNotSent()
    {
        var headers = GrpcRequestHeaders.Build(Greeter, new GrpcRequestHeaderValues(null, null, [":path: /x"]));

        Assert.AreEqual(6, headers.Count);
        Assert.AreEqual("/helloworld.Greeter/SayHello", headers[2].Value);
    }

    [TestMethod]
    [DataRow("trace-bin: ***")]
    [DataRow("trace-bin: A")]
    public void Build_BinaryMetadataNotBase64_FailsWithExit2(string entry)
    {
        GrpcClientException failure = Assert.ThrowsExactly<GrpcClientException>(() => GrpcRequestHeaders.Build(Greeter, new GrpcRequestHeaderValues(null, null, [entry])));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
    }

    [TestMethod]
    [DataRow("ftp", "example.com", "helloworld.Greeter", "SayHello")]
    [DataRow("https", "", "helloworld.Greeter", "SayHello")]
    [DataRow("https", "example.com", "", "SayHello")]
    [DataRow("https", "example.com", "a/b", "SayHello")]
    [DataRow("https", "example.com", "helloworld.Greeter", "")]
    [DataRow("https", "example.com", "helloworld.Greeter", "Say/Hello")]
    public void Build_Requests_InvalidTarget_IsRefused(string scheme, string authority, string service, string method) =>
        Assert.ThrowsExactly<ArgumentException>(() => GrpcRequestHeaders.Build(new GrpcRequestTarget(scheme, authority, service, method), GrpcRequestHeaderValues.None));

    [TestMethod]
    public void Build_Requests_NullTarget_IsRefused() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => GrpcRequestHeaders.Build(null!, GrpcRequestHeaderValues.None));

    [TestMethod]
    public void Build_Requests_NullValues_IsRefused() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => GrpcRequestHeaders.Build(Greeter, null!));
}
