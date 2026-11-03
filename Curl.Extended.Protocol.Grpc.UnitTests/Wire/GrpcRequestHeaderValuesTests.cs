namespace Curl.Extended.Protocol.Grpc.Wire;

/// <summary>Checks what a call's Request-Headers carry from the command line (ADR-1227).</summary>
[TestClass]
public sealed class GrpcRequestHeaderValuesTests
{
    [TestMethod]
    [DataRow(null, "curl/8.22.0")]
    [DataRow("", null)]
    [DataRow("probe/1", "probe/1")]
    public void UserAgentFor_UserAgentOption_IsCurlsOwnNoneOrTheValue(string? option, string? expected) =>
        Assert.AreEqual(expected, GrpcRequestHeaderValues.UserAgentFor(option));

    [TestMethod]
    public void None_Always_AddsNothing()
    {
        GrpcRequestHeaderValues none = GrpcRequestHeaderValues.None;

        Assert.AreEqual((null, null, 0), (none.Timeout, none.UserAgent, none.CustomHeaders.Count));
    }
}
