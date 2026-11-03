using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Brokers.Stomp;

/// <summary>curl's transfer options for <c>stomp://</c> (ADR-1059) on port 61613, in clear.</summary>
[TestClass]
public sealed class StompTransferSurfaceCases
{
    [TestMethod]
    [SpecRequirement("CurlSurface:StompVerbose-1")]
    public Task Verbose_Answered_WritesTheConnectionLineOnStandardErrorAndOnlyTheBodyOnStandardOutput() => StompTransferChecks.VerboseAsync(StompTransferScheme.Stomp);

    [TestMethod]
    [SpecRequirement("CurlSurface:StompOutput-1")]
    public Task Output_Answered_WritesTheBodyToTheFileAndNothingToStandardOutput() => StompTransferChecks.OutputAsync(StompTransferScheme.Stomp);

    [TestMethod]
    [SpecRequirement("CurlSurface:StompRemoteName-1")]
    public Task RemoteName_Answered_WritesTheBodyToTheFileNamedAfterTheUrl() => StompTransferChecks.RemoteNameAsync(StompTransferScheme.Stomp);

    [TestMethod]
    [SpecRequirement("CurlSurface:StompWriteOut-1")]
    public Task WriteOut_Answered_WritesEveryVariableAfterTheBody() => StompTransferChecks.WriteOutAsync(StompTransferScheme.Stomp);

    [TestMethod]
    [SpecRequirement("CurlSurface:StompFail-1")]
    public Task Fail_ErrorFrame_EndsWithExit8AndTheMessage() => StompTransferChecks.FailAsync(StompTransferScheme.Stomp, "--fail", "Fail");

    [TestMethod]
    [SpecRequirement("CurlSurface:StompFailWithBody-1")]
    public Task FailWithBody_ErrorFrame_EndsWithExit8AndTheMessage() => StompTransferChecks.FailAsync(StompTransferScheme.Stomp, "--fail-with-body", "FailWithBody");

    [TestMethod]
    [SpecRequirement("CurlSurface:StompMaxTime-1")]
    public Task MaxTime_ServerNeverAnswers_EndsWithExit28AtTheDeadlineOnTheInjectedClock() => StompTransferChecks.MaxTimeAsync(StompTransferScheme.Stomp);
}
