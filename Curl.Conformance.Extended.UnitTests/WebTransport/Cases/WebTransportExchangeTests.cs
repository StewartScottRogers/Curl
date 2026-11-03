using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.WebTransport.Server;

namespace Curl.Conformance.Extended.UnitTests.WebTransport.Cases;

/// <summary>Sections 4.2, 4.3 and 4.6 and RFC 9297 section 2.1: data on the session's streams and in its datagrams, each naming the session.</summary>
[TestClass]
public sealed class WebTransportExchangeTests
{
    [TestMethod]
    [SpecRequirement("WebTransport:WTH3-4.3-1")]
    public Task Bidirectional_Data_GoesAfterSignal0x41AndTheSessionIdAndTheEchoIsWritten() =>
        WebTransportRun.CaseAsync(["WebTransport:WTH3-4.3-1"], async () =>
        {
            using WebTransportReferenceServer server = WebTransportRun.Server(new WebTransportServerScript { EchoBidirectionalStreams = true });

            CurlRunResult result = await WebTransportRun.RunAsync(server, WebTransportRun.Url, "-d", "hello bidi");

            WebTransportRun.AssertSucceeded(result, "hello bidi");
            WebTransportRecordedStream stream = server.BidirectionalStreams.Single();
            Assert.AreEqual(0L, stream.SessionId);
            Assert.AreEqual("hello bidi", WebTransportRun.Text(stream.Bytes));
            Assert.IsTrue(stream.IsFinished);
            Assert.IsEmpty(server.UnidirectionalStreams);
        });

    [TestMethod]
    [SpecRequirement("WebTransport:WTH3-4.2-1")]
    public Task Unidirectional_Data_GoesAfterType0x54AndTheSessionIdAndTheServerStreamIsWritten() =>
        WebTransportRun.CaseAsync(["WebTransport:WTH3-4.2-1"], async () =>
        {
            using WebTransportReferenceServer server = WebTransportRun.Server(new WebTransportServerScript { EchoUnidirectionalStreams = true });

            CurlRunResult result = await WebTransportRun.RunAsync(server, WebTransportRun.Url + "?wt=uni", "-d", "hello uni");

            WebTransportRun.AssertSucceeded(result, "hello uni");
            WebTransportRecordedStream stream = server.UnidirectionalStreams.Single();
            Assert.AreEqual(0L, stream.SessionId);
            Assert.AreEqual("hello uni", WebTransportRun.Text(stream.Bytes));
            Assert.IsTrue(stream.IsFinished);
            Assert.IsEmpty(server.BidirectionalStreams);
            Assert.AreEqual("/chat", WebTransportRun.ConnectField(server, ":path"));
        });

    [TestMethod]
    [SpecRequirement("WebTransport:WTH3-4.6-1")]
    [SpecRequirement("WebTransport:RFC9297-2.1-1")]
    public Task Datagram_Data_CarriesTheQuarterStreamIdOfTheSessionAndTheEchoIsWritten() =>
        WebTransportRun.CaseAsync(["WebTransport:WTH3-4.6-1", "WebTransport:RFC9297-2.1-1"], async () =>
        {
            using WebTransportReferenceServer server = WebTransportRun.Server(new WebTransportServerScript { EchoDatagrams = true });

            CurlRunResult result = await WebTransportRun.RunAsync(server, WebTransportRun.Url + "?wt=datagram", "-d", "hello datagram");

            WebTransportRun.AssertSucceeded(result, "hello datagram");
            WebTransportRecordedDatagram datagram = server.Datagrams.Single();
            Assert.AreEqual(0L, datagram.QuarterStreamId);
            Assert.AreEqual("hello datagram", WebTransportRun.Text(datagram.Payload));
            Assert.IsEmpty(server.BidirectionalStreams);
            Assert.IsEmpty(server.UnidirectionalStreams);
        });
}
