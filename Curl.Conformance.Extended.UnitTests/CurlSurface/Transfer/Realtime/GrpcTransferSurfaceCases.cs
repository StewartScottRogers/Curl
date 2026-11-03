using Curl.Conformance.Extended.Grpc;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Grpc;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Realtime;

/// <summary>
/// curl's transfer options for <c>grpc://</c> and <c>grpcs://</c> (ADR-1147, ADR-1197): a unary call with
/// <c>--grpc-proto</c> and <c>-d</c>, and a grpc-status other than 0 failing with exit 22 and the grpc-message
/// (ADR-1133, the extension's help line).
/// </summary>
[TestClass]
public sealed class GrpcTransferSurfaceCases
{
    internal static RealtimeTransferScheme For(string scheme) =>
        new(
            scheme,
            scheme == "grpc" ? 50061 : 50062,
            scheme == "grpc" ? RealtimeTransport.Clear : RealtimeTransport.TlsAlpnH2,
            GrpcUnaryCases.MethodPath,
            "SayHello",
            () => new GrpcReferenceServer(new GrpcScriptedService(GrpcUnaryCases.MethodPath, GrpcReferenceReply.Ok([0x0A, 0x04, .. "hi x"u8]))),
            CallArgumentsAsync,
            "{\n  \"message\": \"hi x\"\n}\n",
            () => new GrpcReferenceServer(new GrpcScriptedService(GrpcUnaryCases.MethodPath, GrpcReferenceReply.TrailersOnly(7, "denied"))),
            CallArgumentsAsync,
            "grpc-status 7 (PERMISSION_DENIED): denied");

    private static async Task<string[]> CallArgumentsAsync(string directory)
    {
        string proto = Path.Combine(directory, "greeter.proto");
        await File.WriteAllTextAsync(proto, GrpcUnaryCases.Proto);
        return ["--grpc-proto", proto, "-d", """{"name":"x"}"""];
    }

    [TestMethod]
    [SpecRequirement("CurlSurface:verbose-grpc-1")]
    [SpecRequirement("CurlSurface:verbose-grpcs-1")]
    [DataRow("grpc")]
    [DataRow("grpcs")]
    public Task Verbose_Answered_WritesConnectionAndTlsLinesOnStandardErrorAndOnlyTheBodyOnStandardOutput(string scheme) => RealtimeTransferChecks.VerboseAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:output-grpc-1")]
    [SpecRequirement("CurlSurface:output-grpcs-1")]
    [DataRow("grpc")]
    [DataRow("grpcs")]
    public Task Output_Answered_WritesTheBodyToTheFileAndNothingToStandardOutput(string scheme) => RealtimeTransferChecks.OutputAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:remote-name-grpc-1")]
    [SpecRequirement("CurlSurface:remote-name-grpcs-1")]
    [DataRow("grpc")]
    [DataRow("grpcs")]
    public Task RemoteName_Answered_WritesTheBodyToTheFileNamedAfterTheUrl(string scheme) => RealtimeTransferChecks.RemoteNameAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:write-out-grpc-1")]
    [SpecRequirement("CurlSurface:write-out-grpcs-1")]
    [DataRow("grpc")]
    [DataRow("grpcs")]
    public Task WriteOut_Answered_WritesEveryVariableAfterTheBody(string scheme) => RealtimeTransferChecks.WriteOutAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:fail-grpc-1")]
    [SpecRequirement("CurlSurface:fail-grpcs-1")]
    [DataRow("grpc")]
    [DataRow("grpcs")]
    public Task Fail_ServerError_EndsWithExit22AndTheMessage(string scheme) => RealtimeTransferChecks.FailAsync(For(scheme), "--fail");

    [TestMethod]
    [SpecRequirement("CurlSurface:fail-with-body-grpc-1")]
    [SpecRequirement("CurlSurface:fail-with-body-grpcs-1")]
    [DataRow("grpc")]
    [DataRow("grpcs")]
    public Task FailWithBody_ServerError_EndsWithExit22AndTheMessage(string scheme) => RealtimeTransferChecks.FailAsync(For(scheme), "--fail-with-body");

    [TestMethod]
    [SpecRequirement("CurlSurface:max-time-grpc-1")]
    [SpecRequirement("CurlSurface:max-time-grpcs-1")]
    [DataRow("grpc")]
    [DataRow("grpcs")]
    public Task MaxTime_ServerNeverAnswers_EndsWithExit28AtTheDeadlineOnTheInjectedClock(string scheme) => RealtimeTransferChecks.MaxTimeAsync(For(scheme));
}
