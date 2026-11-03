using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Brokers.Nats;

/// <summary>
/// curl's transfer options for <c>nats://</c> over plain TCP (ADR-1065): a subscription that receives one message, and a
/// <c>-ERR</c> after SUB, which ADR-1065 makes exit 8 with or without <c>--fail</c>.
/// </summary>
[TestClass]
public sealed class NatsTransferSurfaceCases
{
    private const string Scheme = "nats";

    [TestMethod]
    [SpecRequirement("CurlSurface:NatsVerbose-1")]
    public Task Verbose_Answered_WritesTheConnectionLineOnStandardErrorAndOnlyTheBodyOnStandardOutput() =>
        NatsTransferChecks.VerboseAsync(Scheme, "CurlSurface:NatsVerbose-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:NatsOutput-1")]
    public Task Output_Answered_WritesTheBodyToTheFileAndNothingToStandardOutput() =>
        NatsTransferChecks.OutputAsync(Scheme, "CurlSurface:NatsOutput-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:NatsRemoteName-1")]
    public Task RemoteName_Answered_WritesTheBodyToTheFileNamedAfterTheSubject() =>
        NatsTransferChecks.RemoteNameAsync(Scheme, "CurlSurface:NatsRemoteName-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:NatsWriteOut-1")]
    public Task WriteOut_Answered_WritesEveryVariableAfterTheBody() =>
        NatsTransferChecks.WriteOutAsync(Scheme, "CurlSurface:NatsWriteOut-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:NatsFail-1")]
    public Task Fail_ServerErr_EndsWithExit8AndTheServersText() =>
        NatsTransferChecks.FailAsync(Scheme, "CurlSurface:NatsFail-1", "--fail");

    [TestMethod]
    [SpecRequirement("CurlSurface:NatsFailWithBody-1")]
    public Task FailWithBody_ServerErr_EndsWithExit8AndTheServersText() =>
        NatsTransferChecks.FailAsync(Scheme, "CurlSurface:NatsFailWithBody-1", "--fail-with-body");

    [TestMethod]
    [SpecRequirement("CurlSurface:NatsMaxTime-1")]
    public Task MaxTime_ServerNeverAnswersTheHandshake_EndsWithExit28AtTheDeadlineOnTheInjectedClock() =>
        NatsTransferChecks.MaxTimeAsync(Scheme, "CurlSurface:NatsMaxTime-1");
}
