using System.Net;
using System.Text;
using Curl.Core;
using Curl.Extended.Protocol.Xmpp.Client;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Xmpp;

/// <summary>Checks that <c>-T</c> on <c>xmpp://</c> publishes the file's bytes as a message body, as <c>-d</c> does.</summary>
[TestClass]
public sealed class XmppUploadTests
{
    [TestMethod]
    public async Task ExecuteAsync_UploadStreamAndNoPostData_SendsTheFileBytesAsTheMessageBody()
    {
        ScriptedXmppConnection connection = new([.. new[]
        {
            "<?xml version='1.0'?><stream:stream xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams' id='s1' from='example.com' version='1.0'><stream:features><mechanisms xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><mechanism>PLAIN</mechanism></mechanisms></stream:features>",
            "<success xmlns='urn:ietf:params:xml:ns:xmpp-sasl'/>",
            "<?xml version='1.0'?><stream:stream xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams' id='s1' from='example.com' version='1.0'><stream:features><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'/></stream:features>",
            "<iq type='result' id='bind'><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'><jid>alice@example.com/r</jid></bind></iq>",
            "</stream:stream>",
        }.Select(Encoding.UTF8.GetBytes)]) { FailsWhenDrained = true };
        var handler = new XmppProtocolHandler(new ScriptedXmppConnector(ConnectResult.Connected(connection)), new ScriptedTlsProvider(ConnectResult.Refused("no tls")));

        TransferResult result = await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("xmpp://example.com/romeo@example.net"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("alice", "s3cret"),
            Upload = new MemoryStream(Encoding.UTF8.GetBytes("filebody")),
        });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.IsTrue(connection.WrittenText.Contains("<message to=\"romeo@example.net\" xmlns=\"jabber:client\"><body>filebody</body></message>"));
    }
}
