using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins the <c>[HTTP/3]</c> lines <see cref="Http3StreamTrace" /> reports as they happen rather than
/// holding them (BL-1208): the end of a transfer that shares its connection, and nothing at all
/// when it has no events.
/// </summary>
[TestClass]
public sealed class Http3StreamTraceTests
{
    [TestMethod]
    public void TransferDone_WithAnotherStreamInUse_WritesNoNoActiveStreamsLine()
    {
        RecordingTransferEvents events = new();

        new Http3StreamTrace(events).TransferDone(4, 2, 97, 1);

        CollectionAssert.AreEqual(
            new[] { "[HTTP/3] [4] easy handle is done", "[HTTP/3] query conn[2]: MAX_CONCURRENT -> 97 (1 in use)" },
            events.Info);
    }
}
