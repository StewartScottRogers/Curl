using System.Buffers.Binary;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins what <see cref="SmbOpenResponse" /> takes from the measured open response, and its
/// last change time at the edges curl's <c>get_posix_time</c> handles.
/// </summary>
[TestClass]
public sealed class SmbOpenResponseTests
{
    [TestMethod]
    public void TryRead_MeasuredResponse_TakesTheFidSizeAndTime()
    {
        bool accepted = SmbOpenResponse.TryRead(SmbRecordedExchange.OpenAccepted, out SmbOpenResponse? response);

        Assert.IsTrue(accepted);
        Assert.AreEqual(new SmbOpenResponse(0x4001, 11, SmbRecordedExchange.FileLastChangeTimeUtc), response);
    }

    [TestMethod]
    public void TryRead_TimeBefore1970_Is1970()
    {
        SmbOpenResponse response = ReadWithChangeTime(116444736000000000 - 1);

        Assert.AreEqual(DateTimeOffset.UnixEpoch, response.LastChangeTimeUtc);
    }

    [TestMethod]
    public void TryRead_TimePastTheLastRepresentableSecond_IsThatSecond()
    {
        SmbOpenResponse response = ReadWithChangeTime(long.MaxValue);

        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.MaxValue.ToUnixTimeSeconds()), response.LastChangeTimeUtc);
    }

    [TestMethod]
    public void TryRead_ErrorStatus_IsRefused()
    {
        bool accepted = SmbOpenResponse.TryRead(SmbRecordedExchange.OpenMissingFile, out SmbOpenResponse? response);

        Assert.IsFalse(accepted);
        Assert.IsNull(response);
    }

    private static SmbOpenResponse ReadWithChangeTime(long fileTime)
    {
        byte[] opened = SmbRecordedExchange.OpenAccepted;
        BinaryPrimitives.WriteInt64LittleEndian(opened.AsSpan(72), fileTime);
        SmbOpenResponse.TryRead(opened, out SmbOpenResponse? response);
        return response!;
    }
}
