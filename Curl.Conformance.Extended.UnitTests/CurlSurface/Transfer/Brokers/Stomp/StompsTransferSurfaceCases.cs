using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Brokers.Stomp;

/// <summary>curl's transfer options for <c>stomps://</c> (ADR-1059) on port 61614, over TLS with <c>-k</c>.</summary>
[TestClass]
public sealed class StompsTransferSurfaceCases
{
    [TestMethod]
    [SpecRequirement("CurlSurface:StompVerbose-2")]
    public Task Verbose_Answered_WritesConnectionAndTlsLinesOnStandardErrorAndOnlyTheBodyOnStandardOutput() => StompTransferChecks.VerboseAsync(StompTransferScheme.Stomps);

    [TestMethod]
    [SpecRequirement("CurlSurface:StompOutput-2")]
    public Task Output_Answered_WritesTheBodyToTheFileAndNothingToStandardOutput() => StompTransferChecks.OutputAsync(StompTransferScheme.Stomps);

    [TestMethod]
    [SpecRequirement("CurlSurface:StompRemoteName-2")]
    public Task RemoteName_Answered_WritesTheBodyToTheFileNamedAfterTheUrl() => StompTransferChecks.RemoteNameAsync(StompTransferScheme.Stomps);

    [TestMethod]
    [SpecRequirement("CurlSurface:StompWriteOut-2")]
    public Task WriteOut_Answered_WritesEveryVariableAfterTheBody() => StompTransferChecks.WriteOutAsync(StompTransferScheme.Stomps);

    [TestMethod]
    [SpecRequirement("CurlSurface:StompFail-2")]
    public Task Fail_ErrorFrame_EndsWithExit8AndTheMessage() => StompTransferChecks.FailAsync(StompTransferScheme.Stomps, "--fail", "Fail");

    [TestMethod]
    [SpecRequirement("CurlSurface:StompFailWithBody-2")]
    public Task FailWithBody_ErrorFrame_EndsWithExit8AndTheMessage() => StompTransferChecks.FailAsync(StompTransferScheme.Stomps, "--fail-with-body", "FailWithBody");

    [TestMethod]
    [SpecRequirement("CurlSurface:StompMaxTime-2")]
    public Task MaxTime_ServerNeverAnswers_EndsWithExit28AtTheDeadlineOnTheInjectedClock() => StompTransferChecks.MaxTimeAsync(StompTransferScheme.Stomps);
}
