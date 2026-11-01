using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Smtp.Fakes;

namespace Curl.Original.Protocol.Smtp;

/// <summary>
/// CGR-219: a session that fails on a server reply reports that reply's code, as curl's
/// <c>%{response_code}</c> does, so a refused <c>EHLO</c> and <c>HELO</c> read 500, not 000.
/// </summary>
[TestClass]
public sealed class Cgr219Tests
{
    [TestMethod]
    public async Task ExecuteAsync_EhloAndHeloRefused_ReportsTheLastReplyCode()
    {
        var connection = new SmtpProtocolHandlerTwoUrlTests.PoolingConnection(
            SmtpProtocolHandlerTwoUrlTests.Read("220 localhost ESMTP\r\n"), SmtpProtocolHandlerTwoUrlTests.Read("500 no\r\n"),
            SmtpProtocolHandlerTwoUrlTests.Read("500 no\r\n"), SmtpProtocolHandlerTwoUrlTests.Read("221 Bye\r\n"));
        var handler = new SmtpProtocolHandler(new QueuedConnector(ConnectResult.Connected(connection, null)), new QueuedTlsProvider());

        TransferResult result = await handler.ExecuteAsync(SmtpProtocolHandlerTwoUrlTests.Context());

        Assert.AreEqual(CurlExitCode.RemoteAccessDenied, result.ExitCode);
        Assert.AreEqual(500, result.Report?.ResponseCode);
    }
}
