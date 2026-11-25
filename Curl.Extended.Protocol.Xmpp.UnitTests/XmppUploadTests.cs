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
        ScriptedXmppConnection connection = LoggedInConnection();

        TransferResult result = await SendAsync(connection, upload: Encoding.UTF8.GetBytes("filebody"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.Contains(MessageWithBody("filebody"), connection.WrittenText);
    }

    [TestMethod]
    public async Task ExecuteAsync_PostDataOfValidNonAsciiUtf8_SendsItUnchanged()
    {
        ScriptedXmppConnection connection = LoggedInConnection();

        TransferResult result = await SendAsync(connection, postData: Encoding.UTF8.GetBytes("café � \U0001F600"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.Contains(MessageWithBody("café � \U0001F600"), connection.WrittenText);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x63, 0x61, 0x66, 0xE9, 0x20 }, DisplayName = "Latin-1 e acute")]
    [DataRow(new byte[] { 0xFF, 0xFE }, DisplayName = "bytes UTF-8 never uses")]
    [DataRow(new byte[] { 0x63, 0xC3 }, DisplayName = "sequence cut short at the end")]
    [DataRow(new byte[] { 0xC0, 0xAF }, DisplayName = "overlong form")]
    [DataRow(new byte[] { 0xED, 0xA0, 0x80 }, DisplayName = "encoded surrogate")]
    public async Task ExecuteAsync_BodyThatIsNotUtf8_FailsWithBadFunctionArgumentAndSendsNothing(byte[] bytes)
    {
        foreach (bool asUpload in new[] { true, false })
        {
            ScriptedXmppConnection connection = LoggedInConnection();

            TransferResult result = await SendAsync(connection, asUpload ? bytes : null, asUpload ? null : bytes);

            Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.ExitCode);
            Assert.AreEqual("XMPP message body is not valid UTF-8", result.ErrorMessage);
            Assert.IsEmpty(connection.Writes);
        }
    }

    private static string MessageWithBody(string body) =>
        $"<message to=\"romeo@example.net\" xmlns=\"jabber:client\"><body>{body}</body></message>";

    private static ScriptedXmppConnection LoggedInConnection() => new([.. new[]
    {
        "<?xml version='1.0'?><stream:stream xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams' id='s1' from='example.com' version='1.0'><stream:features><mechanisms xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><mechanism>PLAIN</mechanism></mechanisms></stream:features>",
        "<success xmlns='urn:ietf:params:xml:ns:xmpp-sasl'/>",
        "<?xml version='1.0'?><stream:stream xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams' id='s1' from='example.com' version='1.0'><stream:features><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'/></stream:features>",
        "<iq type='result' id='bind'><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'><jid>alice@example.com/r</jid></bind></iq>",
        "</stream:stream>",
    }.Select(Encoding.UTF8.GetBytes)])
    { FailsWhenDrained = true };

    private static async Task<TransferResult> SendAsync(ScriptedXmppConnection connection, byte[]? upload = null, byte[]? postData = null)
    {
        var handler = new XmppProtocolHandler(new ScriptedXmppConnector(ConnectResult.Connected(connection)), new ScriptedTlsProvider(ConnectResult.Refused("no tls")));

        return await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("xmpp://example.com/romeo@example.net"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("alice", "s3cret"),
            Upload = upload is null ? null : new MemoryStream(upload),
            PostData = postData is null ? (ReadOnlyMemory<byte>?)null : postData,
        });
    }
}
