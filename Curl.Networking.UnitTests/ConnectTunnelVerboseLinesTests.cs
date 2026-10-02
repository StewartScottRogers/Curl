using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="ConnectTunnelVerboseLines" />'s edges the <see cref="TcpConnector" /> cases in
/// <c>TcpConnectorTests.ProxyAuthVerbose</c> do not reach: a head with no final line feed, replies
/// that refuse nothing, and the schemes named before a CONNECT (BL-863).
/// </summary>
[TestClass]
public sealed class ConnectTunnelVerboseLinesTests
{
    private const string Establishing = "* Establishing HTTP proxy tunnel to example.test:80";

    [TestMethod]
    public void ReportReplyHead_ReportsALastLineWithoutALineFeedWhole()
    {
        var events = new RecordingTransferEvents();

        ConnectTunnelVerboseLines.ReportReplyHead(events, "HTTP/1.1 407 X\r\nProxy-Authenticate: Basic, basic\tx, Basicx, Digest"u8, 407, "Basic dTpw");

        CollectionAssert.AreEqual(
            new[]
            {
                "< HTTP/1.1 407 X",
                "< Proxy-Authenticate: Basic, basic\tx, Basicx, Digest",
                "* Basic authentication problem, ignoring.",
                "* Basic authentication problem, ignoring.",
            },
            events.Transcript);
    }

    [TestMethod]
    [DataRow(200, "Basic dTpw", DisplayName = "not a 407")]
    [DataRow(407, "NTLM TlRM", DisplayName = "an NTLM value goes on")]
    [DataRow(407, null, DisplayName = "nothing sent")]
    public void ReportReplyHead_ReportsNoProblemWhenTheReplyDoesNotRefuseABasicOrDigestValue(int statusCode, string? authorization)
    {
        var events = new RecordingTransferEvents();

        ConnectTunnelVerboseLines.ReportReplyHead(events, "HTTP/1.1 407 X\r\nProxy-Authenticate: Basic\r\nNo colon here\r\n\r\n"u8, statusCode, authorization);

        Assert.IsFalse(events.Transcript.Any(line => line.StartsWith("* ", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(407, "transfer-encoding: gzip, Chunked\r\n", true, DisplayName = "a 407, chunked last in any case")]
    [DataRow(403, "Transfer-Encoding: chunked", true, DisplayName = "a 403, no line feed")]
    [DataRow(200, "Transfer-Encoding: chunked\r\n", false, DisplayName = "a 2xx")]
    [DataRow(407, "Transfer-Encoding: gzip\r\n", false, DisplayName = "not chunked")]
    [DataRow(407, "X-Transfer-Encoding: chunked\r\n", false, DisplayName = "another field")]
    public void ReportReplyHead_ReportsRespondedChunkedAfterANon2xxChunkedTransferEncodingLine(int statusCode, string field, bool expected)
    {
        // Measured (BL-1144): curl 8.21.0 writes "* CONNECT responded chunked" right after the line.
        var events = new RecordingTransferEvents();

        ConnectTunnelVerboseLines.ReportReplyHead(events, System.Text.Encoding.Latin1.GetBytes($"HTTP/1.1 {statusCode} X\r\n{field}"), statusCode, null);

        CollectionAssert.AreEqual(
            expected
                ? new[] { $"< HTTP/1.1 {statusCode} X", "< " + field.TrimEnd('\r', '\n'), "* CONNECT responded chunked" }
                : new[] { $"< HTTP/1.1 {statusCode} X", "< " + field.TrimEnd('\r', '\n') },
            events.Transcript);
    }

    [TestMethod]
    [DataRow(null, "* chunk reading DONE", DisplayName = "the body ended")]
    [DataRow("chunk hex-length char not a hex digit: 0x7a", "* chunk hex-length char not a hex digit: 0x7a", DisplayName = "a parser message")]
    [DataRow("Proxy CONNECT aborted", "* Proxy CONNECT aborted", DisplayName = "cut short")]
    [DataRow(HttpProxyTunnelChunkedBody.ReceiveFailureMessage, null, DisplayName = "a receive failure")]
    public void ReportChunkedBodyEnd_ReportsWhatCurlWrites(string? failure, string? expected)
    {
        var events = new RecordingTransferEvents();

        ConnectTunnelVerboseLines.ReportChunkedBodyEnd(events, failure);

        CollectionAssert.AreEqual(expected is null ? Array.Empty<string>() : new[] { expected }, events.Transcript);
    }

    [TestMethod]
    [DataRow("NTLM TlRM", false, "* Proxy auth using NTLM with user 'u'", DisplayName = "an NTLM value")]
    [DataRow("Negotiate YII", false, null, DisplayName = "a Negotiate value")]
    [DataRow(null, true, null, DisplayName = "--proxy-digest answering a challenge with nothing")]
    public void ReportBeforeConnect_NamesTheSchemeCurlNames(string? authorization, bool answersChallenge, string? expected)
    {
        var events = new RecordingTransferEvents();
        var request = new HttpAuthRequest("CONNECT", CurlUrl.Parse("http://127.0.0.1:18602/"), "example.test:80", new NetworkCredential("u", "p"), null, HttpAuthSchemes.Digest, IsProxy: true);

        ConnectTunnelVerboseLines.ReportBeforeConnect(events, request, authorization, answersChallenge);

        CollectionAssert.AreEqual(expected is null ? new[] { Establishing } : new[] { expected, Establishing }, events.Transcript);
    }

    [TestMethod]
    public void ReportBeforeConnect_WithDigestAndNoUser_NamesNoScheme()
    {
        var events = new RecordingTransferEvents();
        var request = new HttpAuthRequest("CONNECT", CurlUrl.Parse("http://127.0.0.1:18602/"), "example.test:80", null, null, HttpAuthSchemes.Digest, IsProxy: true);

        ConnectTunnelVerboseLines.ReportBeforeConnect(events, request, null, answersChallenge: false);

        CollectionAssert.AreEqual(new[] { Establishing }, events.Transcript);
    }
}
