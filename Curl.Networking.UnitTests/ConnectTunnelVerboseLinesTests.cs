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
