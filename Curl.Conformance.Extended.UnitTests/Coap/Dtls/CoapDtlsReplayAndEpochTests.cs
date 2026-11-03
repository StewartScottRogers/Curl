using System.Text;
using Curl.Conformance.Extended.Coap.Dtls;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;

namespace Curl.Conformance.Extended.UnitTests.Coap.Dtls;

/// <summary>RFC 6347 sections 4.1 and 4.1.2.6: a replayed record and a record from an old epoch are dropped.</summary>
[TestClass]
public sealed class CoapDtlsReplayAndEpochTests
{
    [TestMethod]
    [SpecRequirement("Coap:RFC6347-4.1.2.6-1")]
    public Task ReplayedSequenceNumberBeforeTheAnswer_CurlDropsItAndWritesTheRealAnswer() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC6347-4.1.2.6-1"], () => AssertForgedRecordDroppedAsync(new DtlsReferenceReplayFault(Forge), 1));

    [TestMethod]
    [SpecRequirement("Coap:RFC6347-4.1-2")]
    public Task Epoch0RecordAfterTheHandshake_CurlDropsItAndWritesTheRealAnswer() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC6347-4.1-2"], () => AssertForgedRecordDroppedAsync(new DtlsReferenceOldEpochFault(Forge), 0));

    /// <summary>The answer with its payload "ok" replaced by "forged".</summary>
    private static byte[] Forge(byte[] answer) => [.. answer[..^2], .. "forged"u8];

    private static async Task AssertForgedRecordDroppedAsync(DtlsReferenceFault fault, ushort forgedEpoch)
    {
        using CoapDtlsFaultRun run = new(fault);

        CurlRunResult result = await CoapDtlsRun.RunAsync(run.Server, "-k");

        Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        Assert.AreEqual("ok", Encoding.UTF8.GetString(result.StandardOutput));
        Assert.IsEmpty(result.StandardError);
        IReadOnlyList<byte[]> sent = result.Exchanges.Single().ServerSent;
        DtlsReferenceRecord forged = DtlsReferenceRecord.Split(sent[^2]).Single();
        Assert.AreEqual(DtlsReferenceRecord.ApplicationData, forged.ContentType);
        Assert.AreEqual(forgedEpoch, forged.Epoch);
        Assert.AreEqual((ushort)1, DtlsReferenceRecord.Split(sent[^1]).Single().Epoch);
    }
}
