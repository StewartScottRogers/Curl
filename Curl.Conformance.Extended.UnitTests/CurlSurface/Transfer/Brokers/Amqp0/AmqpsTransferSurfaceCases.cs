using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Brokers.Amqp0;

/// <summary>curl's transfer options for <c>amqps://</c> (ADR-1091), over TLS on port 5671 trusted with -k, against the AMQP 0-9-1 reference broker.</summary>
[TestClass]
public sealed class AmqpsTransferSurfaceCases
{
    private const string Scheme = "amqps";

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp0Verbose-2")]
    public Task Verbose_Answered_WritesConnectionAndTlsLinesOnStandardErrorAndOnlyTheBodyOnStandardOutput() => Amqp0TransferChecks.VerboseAsync(Scheme);

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp0Output-2")]
    public Task Output_Answered_WritesTheBodyToTheFileAndNothingToStandardOutput() => Amqp0TransferChecks.OutputAsync(Scheme);

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp0RemoteName-2")]
    public Task RemoteName_Answered_WritesTheBodyToTheFileNamedAfterTheQueue() => Amqp0TransferChecks.RemoteNameAsync(Scheme);

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp0WriteOut-2")]
    public Task WriteOut_Answered_WritesEveryVariableAfterTheBody() => Amqp0TransferChecks.WriteOutAsync(Scheme);

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp0Fail-2")]
    public Task Fail_ChannelClosedByTheBroker_EndsWithExit8NotExit22() => Amqp0TransferChecks.FailAsync(Scheme, "--fail", "Fail");

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp0FailWithBody-2")]
    public Task FailWithBody_ChannelClosedByTheBroker_EndsWithExit8NotExit22() => Amqp0TransferChecks.FailAsync(Scheme, "--fail-with-body", "FailWithBody");

    [TestMethod]
    [SpecRequirement("CurlSurface:Amqp0MaxTime-2")]
    public Task MaxTime_BrokerNeverAnswers_EndsWithExit28AtTheDeadlineOnTheInjectedClock() => Amqp0TransferChecks.MaxTimeAsync(Scheme);
}
