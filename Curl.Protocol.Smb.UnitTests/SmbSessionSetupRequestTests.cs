using Curl.Testing;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins <see cref="SmbSessionSetupRequest" /> to curl 8.21.0's 1024-byte limit; its bytes are pinned through the handler.
/// </summary>
[TestClass]
public sealed class SmbSessionSetupRequestTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(948, 1024)]
    [DataRow(949, 0)]
    public void Encode_FitsUpTo1024Bytes(int userLength, int expectedByteCount)
    {
        var identity = new SmbIdentity(new string('u', userLength), "d");
        var negotiate = new SmbNegotiateResponse(new byte[SmbNegotiateResponse.ChallengeLength], 0);

        Diagnostics.Arrange("user length", userLength);
        byte[]? message = SmbSessionSetupRequest.Encode("pw", identity, SmbCurlOperatingSystem.Linux, negotiate);

        Diagnostics.Act("message", message is null ? "null (too large)" : $"{message.Length} bytes: {SmbDiagnostics.Messages(message)}");
        Diagnostics.Assert("byte count", expectedByteCount, message is null ? 0 : message[63] | (message[64] << 8));
        Assert.AreEqual(expectedByteCount, message is null ? 0 : message[63] | (message[64] << 8));
    }
}
