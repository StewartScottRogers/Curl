using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Smtp.Fakes;

namespace Curl.Original.Protocol.Smtp;

/// <summary>
/// CGR-239: a URL that needs another login leaves the first URL's kept connection open, as curl
/// does, and it sends <c>QUIT</c> only when the run's connection cache closes it, after the
/// second URL's session.
/// </summary>
[TestClass]
public sealed class Cgr239Tests
{
    [TestMethod]
    public async Task ExecuteAsync_SecondUploadForAnotherUser_QuitsTheKeptConnectionOnlyWhenTheRunEnds()
    {
        var first = new SmtpProtocolHandlerTwoUrlTests.PoolingConnection(
            SmtpProtocolHandlerTwoUrlTests.Read("220 localhost ESMTP\r\n"), SmtpProtocolHandlerTwoUrlTests.Read("250 localhost\r\n"), SmtpProtocolHandlerTwoUrlTests.Read("250 OK\r\n"),
            SmtpProtocolHandlerTwoUrlTests.Read("250 OK\r\n"), SmtpProtocolHandlerTwoUrlTests.Read("354 Go\r\n"), SmtpProtocolHandlerTwoUrlTests.Read("250 OK\r\n"), SmtpProtocolHandlerTwoUrlTests.Read("221 Bye\r\n"));
        var second = new SmtpProtocolHandlerTwoUrlTests.PoolingConnection(
            SmtpProtocolHandlerTwoUrlTests.Read("220 localhost ESMTP\r\n"), SmtpProtocolHandlerTwoUrlTests.Read("250 localhost\r\n"), SmtpProtocolHandlerTwoUrlTests.Read("250 OK\r\n"),
            SmtpProtocolHandlerTwoUrlTests.Read("250 OK\r\n"), SmtpProtocolHandlerTwoUrlTests.Read("354 Go\r\n"), SmtpProtocolHandlerTwoUrlTests.Read("250 OK\r\n"));
        var connector = new QueuedConnector(
            ConnectResult.Connected(first, null), ConnectResult.Connected(first, null, isReused: true), ConnectResult.Connected(second, null, connectionNumber: 1));
        var handler = new SmtpProtocolHandler(connector, new QueuedTlsProvider());

        await handler.ExecuteAsync(SmtpProtocolHandlerTwoUrlTests.Context("u"));
        TransferResult result = await handler.ExecuteAsync(SmtpProtocolHandlerTwoUrlTests.Context("v"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsFalse(first.IsClosed);
        Assert.AreEqual(SmtpProtocolHandlerTwoUrlTests.Envelope, first.SentText);
        Assert.AreEqual(SmtpProtocolHandlerTwoUrlTests.Envelope, second.SentText);

        await first.CloseAsync();

        Assert.AreEqual(SmtpProtocolHandlerTwoUrlTests.Envelope + "QUIT\r\n", first.SentText);
    }
}
