using System.Buffers.Binary;
using Curl.Testing;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins what <see cref="SmbOpenResponse" /> takes from the measured open response, and its
/// last change time at the edges curl's <c>get_posix_time</c> handles.
/// </summary>
[TestClass]
public sealed class SmbOpenResponseTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void TryRead_MeasuredResponse_TakesTheFidSizeAndTime()
    {
        Diagnostics.Bytes("response", SmbRecordedExchange.OpenAccepted);
        Diagnostics.Arrange("response", SmbDiagnostics.Messages(SmbRecordedExchange.OpenAccepted));

        bool accepted = SmbOpenResponse.TryRead(SmbRecordedExchange.OpenAccepted, out SmbOpenResponse? response);

        Diagnostics.Act("read", $"accepted {accepted}, {response}");
        Diagnostics.Assert("response", new SmbOpenResponse(0x4001, 11, SmbRecordedExchange.FileLastChangeTimeUtc), response);
        Assert.IsTrue(accepted);
        Assert.AreEqual(new SmbOpenResponse(0x4001, 11, SmbRecordedExchange.FileLastChangeTimeUtc), response);
    }

    [TestMethod]
    public void TryRead_TimeBefore1970_Is1970()
    {
        Diagnostics.Arrange("change time", "FILETIME 116444736000000000 - 1, a tick before 1970");

        SmbOpenResponse response = ReadWithChangeTime(116444736000000000 - 1);

        Diagnostics.Act("last change time", response.LastChangeTimeUtc);
        Diagnostics.Assert("last change time", DateTimeOffset.UnixEpoch, response.LastChangeTimeUtc);
        Assert.AreEqual(DateTimeOffset.UnixEpoch, response.LastChangeTimeUtc);
    }

    [TestMethod]
    public void TryRead_TimePastTheLastRepresentableSecond_IsThatSecond()
    {
        Diagnostics.Arrange("change time", "FILETIME long.MaxValue");

        SmbOpenResponse response = ReadWithChangeTime(long.MaxValue);

        Diagnostics.Act("last change time", response.LastChangeTimeUtc);
        Diagnostics.Assert("last change time", DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.MaxValue.ToUnixTimeSeconds()), response.LastChangeTimeUtc);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.MaxValue.ToUnixTimeSeconds()), response.LastChangeTimeUtc);
    }

    [TestMethod]
    public void TryRead_ErrorStatus_IsRefused()
    {
        Diagnostics.Arrange("response", SmbDiagnostics.Messages(SmbRecordedExchange.OpenMissingFile));

        bool accepted = SmbOpenResponse.TryRead(SmbRecordedExchange.OpenMissingFile, out SmbOpenResponse? response);

        Diagnostics.Act("read", $"accepted {accepted}, response {response?.ToString() ?? "null"}");
        Diagnostics.Assert("accepted", false, accepted);
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
