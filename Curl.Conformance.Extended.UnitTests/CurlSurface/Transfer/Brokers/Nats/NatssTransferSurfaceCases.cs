using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Brokers.Nats;

/// <summary>
/// curl's transfer options for <c>natss://</c>, NATS over TLS from the first byte (ADR-1065), against a self-signed
/// certificate with <c>-k</c>: a subscription that receives one message, and a <c>-ERR</c> after SUB, which ADR-1065
/// makes exit 8 with or without <c>--fail</c>.
/// </summary>
[TestClass]
public sealed class NatssTransferSurfaceCases
{
    private const string Scheme = "natss";

    [TestMethod]
    [SpecRequirement("CurlSurface:NatsVerbose-2")]
    public Task Verbose_Answered_WritesConnectionAndTlsLinesOnStandardErrorAndOnlyTheBodyOnStandardOutput() =>
        NatsTransferChecks.VerboseAsync(Scheme, "CurlSurface:NatsVerbose-2");

    [TestMethod]
    [SpecRequirement("CurlSurface:NatsOutput-2")]
    public Task Output_Answered_WritesTheBodyToTheFileAndNothingToStandardOutput() =>
        NatsTransferChecks.OutputAsync(Scheme, "CurlSurface:NatsOutput-2");

    [TestMethod]
    [SpecRequirement("CurlSurface:NatsRemoteName-2")]
    public Task RemoteName_Answered_WritesTheBodyToTheFileNamedAfterTheSubject() =>
        NatsTransferChecks.RemoteNameAsync(Scheme, "CurlSurface:NatsRemoteName-2");

    [TestMethod]
    [SpecRequirement("CurlSurface:NatsWriteOut-2")]
    public Task WriteOut_Answered_WritesEveryVariableAfterTheBody() =>
        NatsTransferChecks.WriteOutAsync(Scheme, "CurlSurface:NatsWriteOut-2");

    [TestMethod]
    [SpecRequirement("CurlSurface:NatsFail-2")]
    public Task Fail_ServerErr_EndsWithExit8AndTheServersText() =>
        NatsTransferChecks.FailAsync(Scheme, "CurlSurface:NatsFail-2", "--fail");

    [TestMethod]
    [SpecRequirement("CurlSurface:NatsFailWithBody-2")]
    public Task FailWithBody_ServerErr_EndsWithExit8AndTheServersText() =>
        NatsTransferChecks.FailAsync(Scheme, "CurlSurface:NatsFailWithBody-2", "--fail-with-body");

    [TestMethod]
    [SpecRequirement("CurlSurface:NatsMaxTime-2")]
    public Task MaxTime_ServerNeverAnswersTheHandshake_EndsWithExit28AtTheDeadlineOnTheInjectedClock() =>
        NatsTransferChecks.MaxTimeAsync(Scheme, "CurlSurface:NatsMaxTime-2");
}
