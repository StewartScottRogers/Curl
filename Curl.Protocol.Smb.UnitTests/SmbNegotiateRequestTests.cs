using Curl.Testing;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins <see cref="SmbNegotiateRequest" /> to the bytes curl sends, measured on 2026-09-29.
/// </summary>
[TestClass]
public sealed class SmbNegotiateRequestTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Encode_IsCurlsMeasuredBytes()
    {
        Diagnostics.Arrange("expected", "curl's measured SMB_COM_NEGOTIATE");

        byte[] encoded = SmbNegotiateRequest.Encode();

        Diagnostics.Act("encoded", SmbDiagnostics.Messages(encoded));
        Diagnostics.Bytes("encoded", encoded);
        Diagnostics.Diff("encoded", SmbRecordedExchange.NegotiateRequest, encoded);
        CollectionAssert.AreEqual(SmbRecordedExchange.NegotiateRequest, SmbNegotiateRequest.Encode());
    }

}
