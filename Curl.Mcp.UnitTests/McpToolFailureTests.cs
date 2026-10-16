using Curl.Protocol.Abstractions;

namespace Curl.Mcp;

[TestClass]
public sealed class McpToolFailureTests
{
    [TestMethod]
    public void FromCurlExit_ExitZero_IsNotAnError()
    {
        Assert.IsNull(McpToolFailure.FromCurlExit(0, "ignored"));
        Assert.IsNull(McpToolFailure.FromCurlExit(CurlExitCode.Ok, "ignored"));
    }

    [TestMethod]
    [DataRow(6, "curl: (6) Could not resolve host: nosuch.invalid", "CouldntResolveHost")]
    [DataRow(7, "curl: (7) Failed to connect to 127.0.0.1 port 1 after 0 ms: Could not connect to server", "CouldntConnect")]
    [DataRow(22, "curl: (22) The requested URL returned error: 404", "HttpReturnedError")]
    [DataRow(28, "curl: (28) Connection timed out after 1001 milliseconds", "OperationTimedOut")]
    [DataRow(35, "curl: (35) schannel: failed to receive handshake, SSL/TLS connection failed", "SslConnectError")]
    public void FromCurlExit_NonzeroExit_CarriesCurlsMessageAndTheCode(int exitCode, string curlMessage, string codeName)
    {
        McpToolFailure? failure = McpToolFailure.FromCurlExit(exitCode, curlMessage + "\n");

        Assert.IsNotNull(failure);
        Assert.AreEqual(McpToolFailureKind.CurlExit, failure.Kind);
        Assert.AreEqual(exitCode, failure.ExitCode);
        Assert.AreEqual($"{curlMessage}\ncurl exit code {exitCode} ({codeName})", failure.Text);
    }

    [TestMethod]
    public void FromCurlExit_EveryCurlExitCode_MapsWithoutThrowing()
    {
        foreach (CurlExitCode exitCode in Enum.GetValues<CurlExitCode>())
        {
            McpToolFailure? failure = McpToolFailure.FromCurlExit(exitCode, "curl: message");

            if (exitCode == CurlExitCode.Ok)
            {
                Assert.IsNull(failure);
                continue;
            }

            Assert.IsNotNull(failure);
            StringAssert.Contains(failure.Text, $"curl exit code {(int)exitCode} (", StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public void FromCurlExit_NumberWithNoName_StatesTheNumberTwice()
    {
        McpToolFailure? failure = McpToolFailure.FromCurlExit(250, "curl: (250) odd");

        Assert.IsNotNull(failure);
        Assert.AreEqual("curl: (250) odd\ncurl exit code 250 (250)", failure.Text);
    }

    [TestMethod]
    public void FromCurlExit_NoMessage_GivesTheCodeLineAlone()
    {
        McpToolFailure? failure = McpToolFailure.FromCurlExit(6, " \r\n");

        Assert.IsNotNull(failure);
        Assert.AreEqual("curl exit code 6 (CouldntResolveHost)", failure.Text);
    }

    [TestMethod]
    public void PathRefused_NamesTheOption()
    {
        McpToolFailure failure = McpToolFailure.PathRefused("--output", "../x lies outside CURL_MCP_ROOT");

        Assert.AreEqual(McpToolFailureKind.PathRefused, failure.Kind);
        Assert.IsNull(failure.ExitCode);
        Assert.AreEqual("Refused --output: ../x lies outside CURL_MCP_ROOT", failure.Text);
    }

    [TestMethod]
    public void Cancelled_IsItsOwnError()
    {
        Assert.AreEqual(McpToolFailureKind.Cancelled, McpToolFailure.Cancelled.Kind);
        Assert.AreEqual(McpToolFailure.CancelledText, McpToolFailure.Cancelled.Text);
        Assert.IsNull(McpToolFailure.Cancelled.ExitCode);
    }
}
