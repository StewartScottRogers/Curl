using System.Text;
using Curl.Conformance.Differential.Protocols.Core;
using Curl.Conformance.Differential.Protocols.Text;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Protocols.Text;

[TestClass]
public sealed class RtspResponderTests
{
    [TestMethod]
    public async Task RespondAsync_Describe_AnswersTheSdpBodyAndEchoesCSeq()
    {
        var request = "DESCRIBE rtsp://h/x RTSP/1.0\r\nCSeq: 1\r\nAccept: application/sdp\r\n\r\n";

        var (output, log) = await RunAsync(request);

        Assert.AreEqual(
            "RTSP/1.0 200 OK\r\nCSeq: 1\r\nPublic: OPTIONS, DESCRIBE\r\nContent-Length: 13\r\nContent-Type: application/sdp\r\n\r\nv=0\r\ns=mock\r\n",
            output);
        Assert.AreEqual(request, log);
    }

    [TestMethod]
    public async Task RespondAsync_OtherMethodWithLowerCaseCSeq_AnswersNoBody()
    {
        var (output, _) = await RunAsync("OPTIONS rtsp://h/x RTSP/1.0\r\ncseq: 7\r\nCSeq: 8\r\n\r\n");

        Assert.AreEqual("RTSP/1.0 200 OK\r\ncseq: 7\r\nPublic: OPTIONS, DESCRIBE\r\n\r\n", output);
    }

    [TestMethod]
    public async Task RespondAsync_NoCSeq_OmitsIt()
    {
        var (output, _) = await RunAsync("TEARDOWN x RTSP/1.0\r\n\r\n");

        Assert.AreEqual("RTSP/1.0 200 OK\r\nPublic: OPTIONS, DESCRIBE\r\n\r\n", output);
    }

    [TestMethod]
    public async Task RespondAsync_TwoRequests_AnswersEachAndLogsBoth()
    {
        var (output, log) = await RunAsync("OPTIONS a RTSP/1.0\r\nCSeq: 1\r\n\r\nPLAY a RTSP/1.0\r\nCSeq: 2\r\n\r\n");

        Assert.AreEqual(
            "RTSP/1.0 200 OK\r\nCSeq: 1\r\nPublic: OPTIONS, DESCRIBE\r\n\r\nRTSP/1.0 200 OK\r\nCSeq: 2\r\nPublic: OPTIONS, DESCRIBE\r\n\r\n",
            output);
        Assert.AreEqual("OPTIONS a RTSP/1.0\r\nCSeq: 1\r\n\r\n\nPLAY a RTSP/1.0\r\nCSeq: 2\r\n\r\n", log);
    }

    [TestMethod]
    public async Task RespondAsync_RequestCutShort_IsStillAnsweredAndLogged()
    {
        var (output, log) = await RunAsync("PLAY a RTSP/1.0\r\nCSeq: 3");

        Assert.AreEqual("RTSP/1.0 200 OK\r\nCSeq: 3\r\nPublic: OPTIONS, DESCRIBE\r\n\r\n", output);
        Assert.AreEqual("PLAY a RTSP/1.0\r\nCSeq: 3", log);
    }

    [TestMethod]
    public async Task RespondAsync_NothingSent_AnswersNothing()
    {
        var (output, log) = await RunAsync(string.Empty);

        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual(string.Empty, log);
    }

    private static async Task<(string Output, string Log)> RunAsync(string input)
    {
        var log = new ProtocolWireLog();
        using var connection = new DuplexMemoryStream(Encoding.Latin1.GetBytes(input));

        await new RtspResponder(log).RespondAsync(connection, CancellationToken.None);

        return (Encoding.Latin1.GetString(connection.Written), Encoding.UTF8.GetString(log.Take()));
    }
}
