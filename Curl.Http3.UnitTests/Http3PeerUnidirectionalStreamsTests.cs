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
    [TestMethod]
    public async Task AcceptAsync_EachCriticalStream_IsSortedAndLeftAfterItsType()
    {
        Http3PeerUnidirectionalStreams streams = new();
        var control = StreamOf("00 04");

        Assert.AreEqual(Http3UnidirectionalStreamType.Control, await streams.AcceptAsync(control, CancellationToken.None));
        Assert.AreEqual(Http3UnidirectionalStreamType.QpackEncoder, await streams.AcceptAsync(StreamOf("02"), CancellationToken.None));
        Assert.AreEqual(Http3UnidirectionalStreamType.QpackDecoder, await streams.AcceptAsync(StreamOf("4003"), CancellationToken.None));
        Assert.AreEqual(1, control.Position);
    }

    [TestMethod]
    [DataRow("21", DisplayName = "a grease type")]
    [DataRow("4054", DisplayName = "an unknown type")]
    [DataRow("", DisplayName = "a stream ending before its type")]
    [DataRow("40", DisplayName = "a stream ending inside its type")]
    public async Task AcceptAsync_StreamToIgnore_GivesNull(string hex) =>
        Assert.IsNull(await new Http3PeerUnidirectionalStreams().AcceptAsync(StreamOf(hex), CancellationToken.None));

    [TestMethod]
    [DataRow("00")]
    [DataRow("02")]
    [DataRow("03")]
    public async Task AcceptAsync_SecondCriticalStream_IsStreamCreationError(string hex)
    {
        Http3PeerUnidirectionalStreams streams = new();
        await streams.AcceptAsync(StreamOf(hex), CancellationToken.None);

        Assert.AreEqual(Http3ErrorCode.StreamCreationError, await ErrorOfAsync(async () => await streams.AcceptAsync(StreamOf(hex), CancellationToken.None)));
    }

    [TestMethod]
    public async Task AcceptAsync_PushStream_IsIdError() =>
        Assert.AreEqual(Http3ErrorCode.IdError, await ErrorOfAsync(async () => await new Http3PeerUnidirectionalStreams().AcceptAsync(StreamOf("01 00"), CancellationToken.None)));

    [TestMethod]
    public async Task AcceptAsync_NullStream_IsRejected() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await new Http3PeerUnidirectionalStreams().AcceptAsync(null!, CancellationToken.None));
}
