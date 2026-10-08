using System.Text;
using Curl.Testing;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins <see cref="SmbWriteRequest" /> against the measured write, and the offset past 4 GiB
/// that curl splits into its low and high words.
/// </summary>
[TestClass]
public sealed class SmbWriteRequestTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Encode_FirstWrite_IsCurlsBytes()
    {
        byte[] data = Encoding.ASCII.GetBytes(SmbRecordedExchange.FileContent);

        Diagnostics.Arrange("encode arguments", "0x0064, 0x0007, 0x4001, 0, data.Length, data");
        byte[] request = SmbWriteRequest.Encode(0x0064, 0x0007, 0x4001, 0, data.Length, data);

        Diagnostics.Act("request", SmbDiagnostics.Messages(request));
        Diagnostics.Bytes("request", request);
        Diagnostics.Diff("request", SmbRecordedExchange.WriteRequest, request);
        CollectionAssert.AreEqual(SmbRecordedExchange.WriteRequest, request);
    }

    [TestMethod]
    public void Encode_FewerBytesThanDeclared_DeclaresTheLengthAndSendsWhatItHas()
    {
        Diagnostics.Arrange("encode arguments", "0x0064, 0x0007, 0x4001, 5, 6, []");
        byte[] request = SmbWriteRequest.Encode(0x0064, 0x0007, 0x4001, 5, 6, []);

        Diagnostics.Act("request", SmbDiagnostics.Messages(request));
        Diagnostics.Bytes("request", request);
        Diagnostics.Diff("request", SmbRecordedExchange.ShortWriteRequest, request);
        CollectionAssert.AreEqual(SmbRecordedExchange.ShortWriteRequest, request);
    }

    [TestMethod]
    public void Encode_OffsetPast4GiB_SplitsItIntoTheLowAndHighWords()
    {
        Diagnostics.Arrange("encode arguments", "0x0064, 0x0007, 0x4001, 0x1_2345_8000, 0, []");
        byte[] request = SmbWriteRequest.Encode(0x0064, 0x0007, 0x4001, 0x1_2345_8000, 0, []);

        Diagnostics.Act("request", SmbDiagnostics.Messages(request));
        Diagnostics.Bytes("request", request);
        Diagnostics.Diff("request[43..47]", new byte[] { 0x00, 0x80, 0x45, 0x23 }, request[43..47]);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x80, 0x45, 0x23 }, request[43..47]);
        Diagnostics.Diff("request[61..65]", new byte[] { 0x01, 0x00, 0x00, 0x00 }, request[61..65]);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x00, 0x00, 0x00 }, request[61..65]);
    }
}
