using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Brokers.Kafka;

/// <summary>
/// curl's transfer options for <c>kafkas://</c> (ADR-1076, port 9093 over TLS, trusted here with <c>-k</c>): consuming partition 0 to its high watermark, and a broker's
/// OFFSET_OUT_OF_RANGE ending the transfer with exit 8 under <c>--fail</c> and <c>--fail-with-body</c> (ADR-1218).
/// </summary>
[TestClass]
public sealed class KafkasTransferSurfaceCases
{
    private static readonly KafkaTransferChecks Checks = new("kafkas", 9093, 2);

    [TestMethod]
    [SpecRequirement("CurlSurface:KafkaVerbose-2")]
    public Task Verbose_Consumed_WritesTheConnectionLineAndTheTlsLinesOnStandardErrorAndOnlyTheRecordsOnStandardOutput() => Checks.VerboseAsync();

    [TestMethod]
    [SpecRequirement("CurlSurface:KafkaOutput-2")]
    public Task Output_Consumed_WritesTheRecordsToTheFileAndNothingToStandardOutput() => Checks.OutputAsync();

    [TestMethod]
    [SpecRequirement("CurlSurface:KafkaRemoteName-2")]
    public Task RemoteName_Consumed_WritesTheRecordsToTheFileNamedAfterThePartition() => Checks.RemoteNameAsync();

    [TestMethod]
    [SpecRequirement("CurlSurface:KafkaWriteOut-2")]
    public Task WriteOut_Consumed_WritesEveryVariableAfterTheRecords() => Checks.WriteOutAsync();

    [TestMethod]
    [SpecRequirement("CurlSurface:KafkaFail-2")]
    public Task Fail_BrokerError_EndsWithExit8AndTheBrokersError() => Checks.FailAsync("--fail", "Fail");

    [TestMethod]
    [SpecRequirement("CurlSurface:KafkaFailWithBody-2")]
    public Task FailWithBody_BrokerError_EndsWithExit8AndTheBrokersError() => Checks.FailAsync("--fail-with-body", "FailWithBody");

    [TestMethod]
    [SpecRequirement("CurlSurface:KafkaMaxTime-2")]
    public Task MaxTime_BrokerNeverAnswers_EndsWithExit28AtTheDeadlineOnTheInjectedClock() => Checks.MaxTimeAsync();
}
