using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.McpClient;
using Curl.Conformance.Extended.UnitTests.McpClient;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Realtime;

/// <summary>
/// curl's transfer options for <c>mcp://</c> and <c>mcps://</c> (ADR-1033, ADR-1074): without <c>-d</c> the tool
/// list, and a tool call whose result has <c>isError</c> writing its content and failing with exit 22, which
/// <c>--fail</c> and <c>--fail-with-body</c> leave as it is: they concern HTTP response codes, and this is a tool's
/// answer inside a 200.
/// </summary>
[TestClass]
public sealed class McpTransferSurfaceCases
{
    private static readonly McpReferenceTool FailingTool = new("fail", "Always fails", """{"content":[{"type":"text","text":"disk full"}],"isError":true}""");

    internal static RealtimeTransferScheme For(string scheme) =>
        new(
            scheme,
            scheme == "mcp" ? 3101 : 3102,
            scheme == "mcp" ? RealtimeTransport.Clear : RealtimeTransport.Tls,
            "/mcp",
            "mcp",
            () => new McpStreamableHttpReferenceServer(new McpReferenceToolServer([McpClientRun.Echo])),
            RealtimeTransferScheme.NoArguments,
            "echo\tEchoes its text\n",
            () => new McpStreamableHttpReferenceServer(new McpReferenceToolServer([FailingTool])),
            directory => Task.FromResult<string[]>(["-d", """{"name":"fail","arguments":{}}"""]),
            "MCP: the tool reported an error",
            "disk full\n");

    [TestMethod]
    [SpecRequirement("CurlSurface:verbose-mcp-1")]
    [SpecRequirement("CurlSurface:verbose-mcps-1")]
    [DataRow("mcp")]
    [DataRow("mcps")]
    public Task Verbose_Answered_WritesConnectionAndTlsLinesOnStandardErrorAndOnlyTheBodyOnStandardOutput(string scheme) => RealtimeTransferChecks.VerboseAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:output-mcp-1")]
    [SpecRequirement("CurlSurface:output-mcps-1")]
    [DataRow("mcp")]
    [DataRow("mcps")]
    public Task Output_Answered_WritesTheBodyToTheFileAndNothingToStandardOutput(string scheme) => RealtimeTransferChecks.OutputAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:remote-name-mcp-1")]
    [SpecRequirement("CurlSurface:remote-name-mcps-1")]
    [DataRow("mcp")]
    [DataRow("mcps")]
    public Task RemoteName_Answered_WritesTheBodyToTheFileNamedAfterTheUrl(string scheme) => RealtimeTransferChecks.RemoteNameAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:write-out-mcp-1")]
    [SpecRequirement("CurlSurface:write-out-mcps-1")]
    [DataRow("mcp")]
    [DataRow("mcps")]
    public Task WriteOut_Answered_WritesEveryVariableAfterTheBody(string scheme) => RealtimeTransferChecks.WriteOutAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:fail-mcp-1")]
    [SpecRequirement("CurlSurface:fail-mcps-1")]
    [DataRow("mcp")]
    [DataRow("mcps")]
    public Task Fail_ServerError_EndsWithExit22AndTheMessage(string scheme) => RealtimeTransferChecks.FailAsync(For(scheme), "--fail");

    [TestMethod]
    [SpecRequirement("CurlSurface:fail-with-body-mcp-1")]
    [SpecRequirement("CurlSurface:fail-with-body-mcps-1")]
    [DataRow("mcp")]
    [DataRow("mcps")]
    public Task FailWithBody_ServerError_EndsWithExit22AndTheMessage(string scheme) => RealtimeTransferChecks.FailAsync(For(scheme), "--fail-with-body");

    [TestMethod]
    [SpecRequirement("CurlSurface:max-time-mcp-1")]
    [SpecRequirement("CurlSurface:max-time-mcps-1")]
    [DataRow("mcp")]
    [DataRow("mcps")]
    public Task MaxTime_ServerNeverAnswers_EndsWithExit28AtTheDeadlineOnTheInjectedClock(string scheme) => RealtimeTransferChecks.MaxTimeAsync(For(scheme));
}
