using Curl.Testing;
using static Curl.Http3.Http3;

namespace Curl.Http3;

/// <summary>
/// Pins <see cref="Http3PeerUnidirectionalStreams" />: sorting the server's unidirectional
/// streams by type (RFC 9114 section 6.2), grease and unknown types ignored, and the
/// streams a client refuses.
/// </summary>
[TestClass]
public sealed class Http3PeerUnidirectionalStreamsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task AcceptAsync_EachCriticalStream_IsSortedAndLeftAfterItsType()
    {
        Diagnostics.Arrange("streams", "00 04 (control), 02 (QPACK encoder), 4003 (QPACK decoder)");
        Http3PeerUnidirectionalStreams streams = new();
        var control = StreamOf("00 04");

        var controlType = await streams.AcceptAsync(control, CancellationToken.None);
        var encoderType = await streams.AcceptAsync(StreamOf("02"), CancellationToken.None);
        var decoderType = await streams.AcceptAsync(StreamOf("4003"), CancellationToken.None);

        Diagnostics.Act("stream types", $"{controlType}, {encoderType}, {decoderType}");
        Diagnostics.Act("control stream position", control.Position);
        Assert.AreEqual(Http3UnidirectionalStreamType.Control, controlType);
        Assert.AreEqual(Http3UnidirectionalStreamType.QpackEncoder, encoderType);
        Assert.AreEqual(Http3UnidirectionalStreamType.QpackDecoder, decoderType);
        Diagnostics.Assert("control stream position", 1L, control.Position);
        Assert.AreEqual(1, control.Position);
    }

    [TestMethod]
    [DataRow("21", DisplayName = "a grease type")]
    [DataRow("4054", DisplayName = "an unknown type")]
    [DataRow("", DisplayName = "a stream ending before its type")]
    [DataRow("40", DisplayName = "a stream ending inside its type")]
    public async Task AcceptAsync_StreamToIgnore_GivesNull(string hex)
    {
        Diagnostics.Arrange("stream", hex);

        var type = await new Http3PeerUnidirectionalStreams().AcceptAsync(StreamOf(hex), CancellationToken.None);

        Diagnostics.Act("stream type", type);
        Diagnostics.Assert("stream type", null, type);
        Assert.IsNull(type);
    }

    [TestMethod]
    [DataRow("00")]
    [DataRow("02")]
    [DataRow("03")]
    public async Task AcceptAsync_SecondCriticalStream_IsStreamCreationError(string hex)
    {
        Diagnostics.Arrange("stream type, twice", hex);
        Http3PeerUnidirectionalStreams streams = new();
        await streams.AcceptAsync(StreamOf(hex), CancellationToken.None);

        var error = await ErrorOfAsync(async () => await streams.AcceptAsync(StreamOf(hex), CancellationToken.None));

        Diagnostics.Act("connection error", error);
        Diagnostics.Assert("connection error", Http3ErrorCode.StreamCreationError, error);
        Assert.AreEqual(Http3ErrorCode.StreamCreationError, error);
    }

    [TestMethod]
    public async Task AcceptAsync_PushStream_IsIdError()
    {
        Diagnostics.Arrange("stream", "01 00 (push stream)");

        var error = await ErrorOfAsync(async () => await new Http3PeerUnidirectionalStreams().AcceptAsync(StreamOf("01 00"), CancellationToken.None));

        Diagnostics.Act("connection error", error);
        Diagnostics.Assert("connection error", Http3ErrorCode.IdError, error);
        Assert.AreEqual(Http3ErrorCode.IdError, error);
    }

    [TestMethod]
    public async Task AcceptAsync_NullStream_IsRejected()
    {
        Diagnostics.Arrange("stream", "null");

        var failure = await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await new Http3PeerUnidirectionalStreams().AcceptAsync(null!, CancellationToken.None));

        Diagnostics.Act("parameter", failure.ParamName);
        Diagnostics.Assert("exception", nameof(ArgumentNullException), failure.GetType().Name);
    }
}
