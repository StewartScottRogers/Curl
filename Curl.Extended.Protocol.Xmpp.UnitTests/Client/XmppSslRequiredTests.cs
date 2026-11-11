using System.Net;
using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Xmpp.Client;

/// <summary>
/// Checks that <c>--ssl-reqd</c> on <c>xmpp://</c> fails with exit 64 before any <c>&lt;auth/&gt;</c> when the server
/// offers no STARTTLS (RFC 6120, 13.8), and that without it a plaintext login carries on as before.
/// </summary>
[TestClass]
public sealed class XmppSslRequiredTests
{
    private const string Header = "<?xml version='1.0'?><stream:stream xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams' id='s1' from='example.com' version='1.0'>";

    private const string PlainOnly = Header + "<stream:features><mechanisms xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><mechanism>PLAIN</mechanism></mechanisms></stream:features>";

    private const string Success = "<success xmlns='urn:ietf:params:xml:ns:xmpp-sasl'/>";

    private const string BindFeatures = Header + "<stream:features><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'/></stream:features>";

    private const string BindResult = "<iq type='result' id='bind'><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'><jid>alice@example.com/r</jid></bind></iq>";

    private static readonly NetworkCredential Alice = new("alice", "s3cret");

    [TestMethod]
    public async Task ExecuteAsync_SslRequiredAndNoStartTlsOffered_FailsWithUseSslFailedBeforeSendingAuth()
    {
        ScriptedXmppConnection connection = ScriptedXmppConnection.Replaying(PlainOnly, Success, BindFeatures, BindResult, "</stream:stream>");

        TransferResult result = await ExecuteAsync(connection, "xmpp://example.com/", TransportSecurityLevel.Required);

        Assert.AreEqual(CurlExitCode.UseSslFailed, result.ExitCode);
        Assert.AreEqual("Requested SSL level failed", result.ErrorMessage);
        Assert.IsFalse(connection.WrittenText.Any(text => text.Contains("<auth", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(TransportSecurityLevel.None)]
    [DataRow(TransportSecurityLevel.Try)]
    public async Task ExecuteAsync_SslNotRequiredAndNoStartTlsOffered_LogsInInPlaintext(TransportSecurityLevel level)
    {
        ScriptedXmppConnection connection = ScriptedXmppConnection.Replaying(PlainOnly, Success, BindFeatures, BindResult, "</stream:stream>");

        TransferResult result = await ExecuteAsync(connection, "xmpp://example.com/romeo@example.net", level, "hi");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.StartsWith("<auth", connection.WrittenText.ElementAt(1));
    }

    [TestMethod]
    public async Task ExecuteAsync_SslRequiredOnASecureConnection_LogsInWithoutStartTls()
    {
        ScriptedXmppConnection connection = new([.. new[] { PlainOnly, Success, BindFeatures, BindResult, "</stream:stream>" }.Select(Encoding.UTF8.GetBytes)]) { IsSecure = true };

        TransferResult result = await ExecuteAsync(connection, "xmpps://example.com/romeo@example.net", TransportSecurityLevel.Required, "hi");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.StartsWith("<auth", connection.WrittenText.ElementAt(1));
    }

    private static ValueTask<TransferResult> ExecuteAsync(ScriptedXmppConnection connection, string url, TransportSecurityLevel level, string? body = null) =>
        new XmppProtocolHandler(new ScriptedXmppConnector(ConnectResult.Connected(connection)), new ScriptedTlsProvider(ConnectResult.Refused("no tls")))
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse(url),
                Output = new MemoryStream(),
                Credentials = Alice,
                SslLevel = level,
                PostData = body is null ? null : (ReadOnlyMemory<byte>?)Encoding.UTF8.GetBytes(body),
            });
}
