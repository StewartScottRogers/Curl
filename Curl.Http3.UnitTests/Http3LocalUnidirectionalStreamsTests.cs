using Curl.Testing;
using static Curl.Http3.Qpack;

namespace Curl.Http3;

/// <summary>
/// Pins <see cref="Http3LocalUnidirectionalStreams" />: the bytes curl's control, QPACK
/// encoder and QPACK decoder streams start with (RFC 9114 section 6.2, RFC 9204 section 4.2).
/// </summary>
[TestClass]
public sealed class Http3LocalUnidirectionalStreamsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task OpenControlStreamAsync_CurlSettings_WritesNghttp3sDefaults()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("settings", string.Join(", ", Http3LocalUnidirectionalStreams.CurlSettings));

        // Stream type 0x00, then SETTINGS (0x04, 13 bytes): MAX_FIELD_SECTION_SIZE (0x06) 2^62 - 1,
        // QPACK_MAX_TABLE_CAPACITY (0x01) 0 and QPACK_BLOCKED_STREAMS (0x07) 0, as nghttp3 writes them.
        MemoryStream control = new();

        await Http3LocalUnidirectionalStreams.OpenControlStreamAsync(control, Http3LocalUnidirectionalStreams.CurlSettings, CancellationToken.None);

        diagnostics.Bytes("control stream", control.ToArray());
        diagnostics.Act("control stream length", control.Length);
        diagnostics.Diff("control stream", FromHex("00 04 0d 06 ffffffffffffffff 01 00 07 00"), control.ToArray());
        CollectionAssert.AreEqual(FromHex("00 04 0d 06 ffffffffffffffff 01 00 07 00"), control.ToArray());
    }

    [TestMethod]
    public async Task OpenQpackStreamsAsync_WriteTheirStreamTypes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("streams", "QPACK encoder (type 0x02), QPACK decoder (type 0x03)");
        MemoryStream encoder = new();
        MemoryStream decoder = new();

        await Http3LocalUnidirectionalStreams.OpenQpackEncoderStreamAsync(encoder, CancellationToken.None);
        await Http3LocalUnidirectionalStreams.OpenQpackDecoderStreamAsync(decoder, CancellationToken.None);

        diagnostics.Bytes("encoder stream", encoder.ToArray());
        diagnostics.Bytes("decoder stream", decoder.ToArray());
        diagnostics.Act("stream lengths", $"encoder {encoder.Length}, decoder {decoder.Length}");
        diagnostics.Diff("encoder stream", FromHex("02"), encoder.ToArray());
        diagnostics.Diff("decoder stream", FromHex("03"), decoder.ToArray());
        CollectionAssert.AreEqual(FromHex("02"), encoder.ToArray());
        CollectionAssert.AreEqual(FromHex("03"), decoder.ToArray());
    }

    [TestMethod]
    public async Task OpenStreamsAsync_NullStream_IsRejected()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("stream", "null, to OpenControlStreamAsync and OpenQpackEncoderStreamAsync");

        var control = await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await Http3LocalUnidirectionalStreams.OpenControlStreamAsync(null!, [], CancellationToken.None));
        var encoder = await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await Http3LocalUnidirectionalStreams.OpenQpackEncoderStreamAsync(null!, CancellationToken.None));

        diagnostics.Act("parameters", $"{control.ParamName}, {encoder.ParamName}");
        diagnostics.Assert("exceptions", "ArgumentNullException x2", "as expected");
    }
}
