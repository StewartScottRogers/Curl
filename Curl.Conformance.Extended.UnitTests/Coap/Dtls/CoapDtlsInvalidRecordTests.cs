using System.Text;
using Curl.Conformance.Extended.Coap.Dtls;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;

namespace Curl.Conformance.Extended.UnitTests.Coap.Dtls;

/// <summary>RFC 6347 section 4.1.2.7: a record that fails authentication is discarded silently, and the association goes on.</summary>
[TestClass]
public sealed class CoapDtlsInvalidRecordTests
{
    [TestMethod]
    [SpecRequirement("Coap:RFC6347-4.1.2.7-1")]
    public Task RecordThatFailsAuthenticationBeforeTheAnswer_CurlDropsItSilentlyAndWritesTheRealAnswer() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC6347-4.1.2.7-1"], async () =>
        {
            using CoapDtlsFaultRun run = new(new DtlsReferenceCorruptRecordFault(answer => [.. answer[..^2], .. "forged"u8]));

            CurlRunResult result = await CoapDtlsRun.RunAsync(run.Server, "-k");

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("ok", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.IsEmpty(result.StandardError);
            Assert.IsFalse(run.Server.Received.Any(record => record.ContentType == DtlsReferenceRecord.Alert));
        });
}
