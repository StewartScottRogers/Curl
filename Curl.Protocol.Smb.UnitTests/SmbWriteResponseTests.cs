using Curl.Testing;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins <see cref="SmbWriteResponse" />: the count of an accepted response, and curl's two
/// refusals, an error status and a response too short for the count.
/// </summary>
[TestClass]
public sealed class SmbWriteResponseTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void TryReadCount_Accepted_ReadsTheCount()
    {
        Diagnostics.Arrange("response", "write accepted, count 0x1c41");

        bool accepted = SmbWriteResponse.TryReadCount(SmbRecordedExchange.WriteAccepted(0x1c41), out int count);

        Diagnostics.Act("read", $"accepted {accepted}, count {count}");
        Diagnostics.Assert("count", 0x1c41, count);
        Assert.IsTrue(accepted);
        Assert.AreEqual(0x1c41, count);
    }

    [TestMethod]
    public void TryReadCount_ErrorStatus_Refuses()
    {
        Diagnostics.Arrange("response", SmbDiagnostics.Messages(SmbRecordedExchange.WriteRefused));

        bool accepted = SmbWriteResponse.TryReadCount(SmbRecordedExchange.WriteRefused, out int count);

        Diagnostics.Act("read", $"accepted {accepted}, count {count}");
        Diagnostics.Assert("accepted", false, accepted);
        Assert.IsFalse(accepted);
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void TryReadCount_OneByteShortOfTheCount_Refuses()
    {
        Diagnostics.Arrange("response", "write accepted, count 11, cut to 41 bytes");

        bool accepted = SmbWriteResponse.TryReadCount(SmbRecordedExchange.WriteAccepted(11).AsSpan(0, 41), out _);

        Diagnostics.Act("accepted", accepted);
        Diagnostics.Assert("accepted", false, accepted);
        Assert.IsFalse(accepted);
    }
}
