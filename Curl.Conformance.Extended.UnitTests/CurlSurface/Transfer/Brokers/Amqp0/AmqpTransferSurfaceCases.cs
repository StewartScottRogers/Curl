using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Brokers.Amqp0;

/// <summary>curl's transfer options for <c>amqp://</c> (ADR-1091), in clear on port 5672, against the AMQP 0-9-1 reference broker.</summary>
[TestClass]
public sealed class AmqpTransferSurfaceCases
{
    private const string Scheme = "amqp";

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp0Verbose-1")]
    public Task Verbose_Answered_WritesConnectionLinesOnStandardErrorAndOnlyTheBodyOnStandardOutput() => Amqp0TransferChecks.VerboseAsync(Scheme);

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp0Output-1")]
    public Task Output_Answered_WritesTheBodyToTheFileAndNothingToStandardOutput() => Amqp0TransferChecks.OutputAsync(Scheme);

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp0RemoteName-1")]
    public Task RemoteName_Answered_WritesTheBodyToTheFileNamedAfterTheQueue() => Amqp0TransferChecks.RemoteNameAsync(Scheme);

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp0WriteOut-1")]
    public Task WriteOut_Answered_WritesEveryVariableAfterTheBody() => Amqp0TransferChecks.WriteOutAsync(Scheme);

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp0Fail-1")]
    public Task Fail_ChannelClosedByTheBroker_EndsWithExit8NotExit22() => Amqp0TransferChecks.FailAsync(Scheme, "--fail", "Fail");

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp0FailWithBody-1")]
    public Task FailWithBody_ChannelClosedByTheBroker_EndsWithExit8NotExit22() => Amqp0TransferChecks.FailAsync(Scheme, "--fail-with-body", "FailWithBody");

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp0MaxTime-1")]
    public Task MaxTime_BrokerNeverAnswers_EndsWithExit28AtTheDeadlineOnTheInjectedClock() => Amqp0TransferChecks.MaxTimeAsync(Scheme);
}
