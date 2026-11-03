using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.SocketIo;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Realtime;

/// <summary>
/// curl's transfer options for <c>socketio://</c> and <c>socketios://</c> (ADR-1035): a connection to the
/// <c>/admin</c> namespace that writes its one event, and a refused namespace failing with exit 22.
/// </summary>
[TestClass]
public sealed class SocketIoTransferSurfaceCases
{
    internal static RealtimeTransferScheme For(string scheme) =>
        new(
            scheme,
            scheme == "socketio" ? 5201 : 5202,
            scheme == "socketio" ? RealtimeTransport.Clear : RealtimeTransport.Tls,
            "/admin",
            "admin",
            () => new SocketIoReferenceServer(
                SocketIoStep.Open(),
                SocketIoStep.Receive,
                SocketIoStep.Packet("""40/admin,{"sid":"oSO0OpakMV_3jnilAAAA"}"""),
                SocketIoStep.Packet("""42/admin,["admin event"]"""),
                SocketIoStep.Packet("41/admin,")),
            RealtimeTransferScheme.NoArguments,
            "[\"admin event\"]\n",
            () => new SocketIoReferenceServer(SocketIoStep.Open(), SocketIoStep.Receive, SocketIoStep.Packet("""44/admin,{"message":"Not authorized"}""")),
            RealtimeTransferScheme.NoArguments,
            """Socket.IO server refused namespace /admin: {"message":"Not authorized"}""");

    [TestMethod]
    [SpecRequirement("CurlSurface:verbose-socketio-1")]
    [SpecRequirement("CurlSurface:verbose-socketios-1")]
    [DataRow("socketio")]
    [DataRow("socketios")]
    public Task Verbose_Answered_WritesConnectionAndTlsLinesOnStandardErrorAndOnlyTheBodyOnStandardOutput(string scheme) => RealtimeTransferChecks.VerboseAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:output-socketio-1")]
    [SpecRequirement("CurlSurface:output-socketios-1")]
    [DataRow("socketio")]
    [DataRow("socketios")]
    public Task Output_Answered_WritesTheBodyToTheFileAndNothingToStandardOutput(string scheme) => RealtimeTransferChecks.OutputAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:remote-name-socketio-1")]
    [SpecRequirement("CurlSurface:remote-name-socketios-1")]
    [DataRow("socketio")]
    [DataRow("socketios")]
    public Task RemoteName_Answered_WritesTheBodyToTheFileNamedAfterTheUrl(string scheme) => RealtimeTransferChecks.RemoteNameAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:write-out-socketio-1")]
    [SpecRequirement("CurlSurface:write-out-socketios-1")]
    [DataRow("socketio")]
    [DataRow("socketios")]
    public Task WriteOut_Answered_WritesEveryVariableAfterTheBody(string scheme) => RealtimeTransferChecks.WriteOutAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:fail-socketio-1")]
    [SpecRequirement("CurlSurface:fail-socketios-1")]
    [DataRow("socketio")]
    [DataRow("socketios")]
    public Task Fail_ServerError_EndsWithExit22AndTheMessage(string scheme) => RealtimeTransferChecks.FailAsync(For(scheme), "--fail");

    [TestMethod]
    [SpecRequirement("CurlSurface:fail-with-body-socketio-1")]
    [SpecRequirement("CurlSurface:fail-with-body-socketios-1")]
    [DataRow("socketio")]
    [DataRow("socketios")]
    public Task FailWithBody_ServerError_EndsWithExit22AndTheMessage(string scheme) => RealtimeTransferChecks.FailAsync(For(scheme), "--fail-with-body");

    [TestMethod]
    [SpecRequirement("CurlSurface:max-time-socketio-1")]
    [SpecRequirement("CurlSurface:max-time-socketios-1")]
    [DataRow("socketio")]
    [DataRow("socketios")]
    public Task MaxTime_ServerNeverAnswers_EndsWithExit28AtTheDeadlineOnTheInjectedClock(string scheme) => RealtimeTransferChecks.MaxTimeAsync(For(scheme));
}
