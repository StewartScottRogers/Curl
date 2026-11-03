using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Brokers.Amqp1;

/// <summary>
/// curl's transfer options for <c>amqp1s://</c> over TLS on 5671, trusted with <c>-k</c> (ADR-1062): a receive from the address <c>queue</c> that the reference
/// container answers with one message, and a link it refuses with amqp:not-found for <c>--fail</c> (ADR-1219).
/// </summary>
[TestClass]
public sealed class Amqp1sTransferSurfaceCases
{
    private static readonly Amqp1TransferScheme Scheme = Amqp1TransferScheme.Amqp1s;

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp1Verbose-2")]
    public Task Verbose_Answered_WritesConnectionLinesOnStandardErrorAndOnlyTheBodyOnStandardOutput() => Amqp1TransferChecks.VerboseAsync(Scheme);

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp1Output-2")]
    public Task Output_Answered_WritesTheBodyToTheFileAndNothingToStandardOutput() => Amqp1TransferChecks.OutputAsync(Scheme);

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp1RemoteName-2")]
    public Task RemoteName_Answered_WritesTheBodyToTheFileNamedAfterTheAddress() => Amqp1TransferChecks.RemoteNameAsync(Scheme);

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp1WriteOut-2")]
    public Task WriteOut_Answered_WritesEveryVariableAfterTheBody() => Amqp1TransferChecks.WriteOutAsync(Scheme);

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp1Fail-2")]
    public Task Fail_LinkRefused_EndsWithTheExtensionsExit78AndItsMessage() => Amqp1TransferChecks.FailAsync(Scheme, "--fail", "Fail");

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp1FailWithBody-2")]
    public Task FailWithBody_LinkRefused_EndsWithTheExtensionsExit78AndItsMessage() => Amqp1TransferChecks.FailAsync(Scheme, "--fail-with-body", "FailWithBody");

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp1MaxTime-2")]
    public Task MaxTime_ServerNeverAnswers_EndsWithExit28AtTheDeadlineOnTheInjectedClock() => Amqp1TransferChecks.MaxTimeAsync(Scheme);
}
