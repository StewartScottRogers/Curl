using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.SignalR;
using Curl.Conformance.Extended.UnitTests.SignalR;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Realtime;

/// <summary>
/// curl's transfer options for <c>signalr://</c> and <c>signalrs://</c> (ADR-1038, ADR-1084): negotiate, then the
/// <c>-d</c> invocation and its completion's result, and a completion with an error failing with exit 22.
/// </summary>
[TestClass]
public sealed class SignalRTransferSurfaceCases
{
    internal static RealtimeTransferScheme For(string scheme) =>
        new(
            scheme,
            scheme == "signalr" ? 5101 : 5102,
            scheme == "signalr" ? RealtimeTransport.Clear : RealtimeTransport.Tls,
            "/hub",
            "hub",
            () => SignalRRun.CallHub(SignalRHubStep.Records("""{"type":3,"invocationId":"1","result":{"sum":3}}""")),
            CallArgumentsAsync,
            "{\"sum\":3}\n",
            () => SignalRRun.CallHub(SignalRHubStep.Records("""{"type":3,"invocationId":"1","error":"Add is not allowed"}""")),
            CallArgumentsAsync,
            "SignalR hub method failed: Add is not allowed");

    private static Task<string[]> CallArgumentsAsync(string directory) => Task.FromResult<string[]>(["-d", SignalRRun.Call]);

    [TestMethod]
    [SpecRequirement("CurlSurface:verbose-signalr-1")]
    [SpecRequirement("CurlSurface:verbose-signalrs-1")]
    [DataRow("signalr")]
    [DataRow("signalrs")]
    public Task Verbose_Answered_WritesConnectionAndTlsLinesOnStandardErrorAndOnlyTheBodyOnStandardOutput(string scheme) => RealtimeTransferChecks.VerboseAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:output-signalr-1")]
    [SpecRequirement("CurlSurface:output-signalrs-1")]
    [DataRow("signalr")]
    [DataRow("signalrs")]
    public Task Output_Answered_WritesTheBodyToTheFileAndNothingToStandardOutput(string scheme) => RealtimeTransferChecks.OutputAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:remote-name-signalr-1")]
    [SpecRequirement("CurlSurface:remote-name-signalrs-1")]
    [DataRow("signalr")]
    [DataRow("signalrs")]
    public Task RemoteName_Answered_WritesTheBodyToTheFileNamedAfterTheUrl(string scheme) => RealtimeTransferChecks.RemoteNameAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:write-out-signalr-1")]
    [SpecRequirement("CurlSurface:write-out-signalrs-1")]
    [DataRow("signalr")]
    [DataRow("signalrs")]
    public Task WriteOut_Answered_WritesEveryVariableAfterTheBody(string scheme) => RealtimeTransferChecks.WriteOutAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:fail-signalr-1")]
    [SpecRequirement("CurlSurface:fail-signalrs-1")]
    [DataRow("signalr")]
    [DataRow("signalrs")]
    public Task Fail_ServerError_EndsWithExit22AndTheMessage(string scheme) => RealtimeTransferChecks.FailAsync(For(scheme), "--fail");

    [TestMethod]
    [SpecRequirement("CurlSurface:fail-with-body-signalr-1")]
    [SpecRequirement("CurlSurface:fail-with-body-signalrs-1")]
    [DataRow("signalr")]
    [DataRow("signalrs")]
    public Task FailWithBody_ServerError_EndsWithExit22AndTheMessage(string scheme) => RealtimeTransferChecks.FailAsync(For(scheme), "--fail-with-body");

    [TestMethod]
    [SpecRequirement("CurlSurface:max-time-signalr-1")]
    [SpecRequirement("CurlSurface:max-time-signalrs-1")]
    [DataRow("signalr")]
    [DataRow("signalrs")]
    public Task MaxTime_ServerNeverAnswers_EndsWithExit28AtTheDeadlineOnTheInjectedClock(string scheme) => RealtimeTransferChecks.MaxTimeAsync(For(scheme));
}
