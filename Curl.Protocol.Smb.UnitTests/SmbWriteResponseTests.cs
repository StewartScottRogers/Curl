namespace Curl.Protocol.Smb;

/// <summary>
/// Pins <see cref="SmbWriteResponse" />: the count of an accepted response, and curl's two
/// refusals, an error status and a response too short for the count.
/// </summary>
[TestClass]
public sealed class SmbWriteResponseTests
{
    [TestMethod]
    public void TryReadCount_Accepted_ReadsTheCount()
    {
        bool accepted = SmbWriteResponse.TryReadCount(SmbRecordedExchange.WriteAccepted(0x1c41), out int count);

        Assert.IsTrue(accepted);
        Assert.AreEqual(0x1c41, count);
    }

    [TestMethod]
    public void TryReadCount_ErrorStatus_Refuses()
    {
        bool accepted = SmbWriteResponse.TryReadCount(SmbRecordedExchange.WriteRefused, out int count);

        Assert.IsFalse(accepted);
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void TryReadCount_OneByteShortOfTheCount_Refuses()
    {
        bool accepted = SmbWriteResponse.TryReadCount(SmbRecordedExchange.WriteAccepted(11).AsSpan(0, 41), out _);

        Assert.IsFalse(accepted);
    }
}
