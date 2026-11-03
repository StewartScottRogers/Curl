using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Brokers.Kafka;

/// <summary>
/// curl's transfer options for <c>kafka://</c> (ADR-1076): consuming partition 0 to its high watermark, and a broker's
/// OFFSET_OUT_OF_RANGE ending the transfer with exit 8 under <c>--fail</c> and <c>--fail-with-body</c> (ADR-1217).
/// </summary>
[TestClass]
public sealed class KafkaTransferSurfaceCases
{
    private static readonly KafkaTransferChecks Checks = new("kafka", 9092, 1);

    [TestMethod]
    [SpecRequirement("CurlSurface:KafkaVerbose-1")]
    public Task Verbose_Consumed_WritesTheConnectionLineOnStandardErrorAndOnlyTheRecordsOnStandardOutput() => Checks.VerboseAsync();

    [TestMethod]
    [SpecRequirement("CurlSurface:KafkaOutput-1")]
    public Task Output_Consumed_WritesTheRecordsToTheFileAndNothingToStandardOutput() => Checks.OutputAsync();

    [TestMethod]
    [SpecRequirement("CurlSurface:KafkaRemoteName-1")]
    public Task RemoteName_Consumed_WritesTheRecordsToTheFileNamedAfterThePartition() => Checks.RemoteNameAsync();

    [TestMethod]
    [SpecRequirement("CurlSurface:KafkaWriteOut-1")]
    public Task WriteOut_Consumed_WritesEveryVariableAfterTheRecords() => Checks.WriteOutAsync();

    [TestMethod]
    [SpecRequirement("CurlSurface:KafkaFail-1")]
    public Task Fail_BrokerError_EndsWithExit8AndTheBrokersError() => Checks.FailAsync("--fail", "Fail");

    [TestMethod]
    [SpecRequirement("CurlSurface:KafkaFailWithBody-1")]
    public Task FailWithBody_BrokerError_EndsWithExit8AndTheBrokersError() => Checks.FailAsync("--fail-with-body", "FailWithBody");

    [TestMethod]
    [SpecRequirement("CurlSurface:KafkaMaxTime-1")]
    public Task MaxTime_BrokerNeverAnswers_EndsWithExit28AtTheDeadlineOnTheInjectedClock() => Checks.MaxTimeAsync();
}
