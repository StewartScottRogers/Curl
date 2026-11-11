using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins that the data event of a body read comes before the body is written, so a trace and the
/// body sharing standard output show the entry first, as curl does (BL-4924).
/// </summary>
[TestClass]
public sealed class HttpResponseBodyReaderTraceOrderTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task CopyAsync_BodyReadAfterTheHead_ReportsItsDataBeforeWritingIt()
    {
        Diagnostics.Arrange("body", "hello /a\n read to close in one read, after the head");
        ScriptedConnection connection = new("hello /a\n"u8.ToArray(), 65536);
        RecordingTransferEvents events = new();
        EventCountingStream output = new(events);
        HttpResponseBodyReader reader = new(connection) { Events = events };
        HttpResponseHead head = new(HttpStatusLine.Parse("HTTP/1.1 200 OK"), [], default, default);

        await reader.CopyAsync(head, false, output, false, false, CancellationToken.None);

        Diagnostics.Assert("data events reported before the first write", 1, output.EventsAtFirstWrite);
        Assert.AreEqual(1, output.EventsAtFirstWrite);
    }

    private sealed class EventCountingStream(RecordingTransferEvents events) : MemoryStream
    {
        public int? EventsAtFirstWrite { get; private set; }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            EventsAtFirstWrite ??= events.Events.Count;
            return base.WriteAsync(buffer, cancellationToken);
        }
    }
}
