using System.Net;
using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Xmpp.Client;

/// <summary>
/// Checks the client exchange against RFC 6120 (4.2, 4.4, 4.9, 5.4, 6.4, 7.6, 8.3), RFC 6121 5.2, RFC 5122 2.2 and
/// XEP-0030 3.1 and 4.1, over recorded server bytes.
/// </summary>
[TestClass]
public sealed class XmppProtocolHandlerTests
{
    private const string Header = "<?xml version='1.0'?><stream:stream xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams' id='s1' from='example.com' version='1.0'>";

    private const string Success = "<success xmlns='urn:ietf:params:xml:ns:xmpp-sasl'/>";

    private const string BindFeatures = Header + "<stream:features><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'/></stream:features>";

    private const string BindResult = "<iq type='result' id='bind'><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'><jid>juliet@example.com/r</jid></bind></iq>";

    private const string Close = "</stream:stream>";

    private const string ClientHeader = "<?xml version='1.0'?><stream:stream to='example.com' version='1.0' xml:lang='en' xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'>";

    private const string BindRequest = "<iq type=\"set\" id=\"bind\" xmlns=\"jabber:client\"><bind xmlns=\"urn:ietf:params:xml:ns:xmpp-bind\" /></iq>";

    private static readonly NetworkCredential Juliet = new("juliet", "secret");

    [TestMethod]
    public async Task ExecuteAsync_Body_LogsInWithPlainBindsSendsTheMessageAndClosesTheStream()
    {
        ScriptedXmppConnection connection = Script(true, [Features("PLAIN"), Success, BindFeatures, BindResult, Close]);

        TransferResult result = await ExecuteAsync(connection, "xmpp://example.com/romeo%40example.net", Juliet, body: "hi");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[]
            {
                ClientHeader,
                "<auth mechanism=\"PLAIN\" xmlns=\"urn:ietf:params:xml:ns:xmpp-sasl\">AGp1bGlldABzZWNyZXQ=</auth>",
                ClientHeader,
                BindRequest,
                "<message to=\"romeo@example.net\" xmlns=\"jabber:client\"><body>hi</body></message>",
                Close,
            },
            connection.WrittenText.ToArray());
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_MessageQueryWithoutUser_LogsInAnonymouslyAndSendsTheQuerysKeys()
    {
        ScriptedXmppConnection connection = Script(false, [Features("ANONYMOUS"), Success, BindFeatures, BindResult, "<presence/>", Close]);

        TransferResult result = await ExecuteAsync(connection, "xmpp://example.com/romeo@example.net?message;subject=Hi%20there;type=chat;body=x;flag", null);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual("<auth mechanism=\"ANONYMOUS\" xmlns=\"urn:ietf:params:xml:ns:xmpp-sasl\">=</auth>", connection.WrittenText.ElementAt(1));
        Assert.AreEqual(
            "<message to=\"romeo@example.net\" type=\"chat\" xmlns=\"jabber:client\"><subject>Hi there</subject><body>x</body></message>",
            connection.WrittenText.ElementAt(4));
    }

    [TestMethod]
    public async Task ExecuteAsync_NoQueryNoBody_AsksTheDomainForDiscoInfoAndWritesTheAnswersPayload()
    {
        ScriptedXmppConnection connection = Script(true, [
            Features("PLAIN"), Success, BindFeatures, BindResult, "<presence/>", "<iq type='get' id='other'/>",
            "<iq type='result' id='disco'><query xmlns='http://jabber.org/protocol/disco#info'><identity category='server' type='im'/></query></iq>", Close]);
        MemoryStream output = new();

        TransferResult result = await ExecuteAsync(connection, "xmpp://example.com", Juliet, output);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        string expected = "<query xmlns=\"http://jabber.org/protocol/disco#info\"><identity category=\"server\" type=\"im\" /></query>\n";
        Assert.AreEqual(expected, Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual(expected.Length, result.BytesTransferred);
        Assert.AreEqual("<iq type=\"get\" id=\"disco\" to=\"example.com\" xmlns=\"jabber:client\"><query xmlns=\"http://jabber.org/protocol/disco#info\" /></iq>", connection.WrittenText.ElementAt(4));
    }

    [TestMethod]
    public async Task ExecuteAsync_DiscoItemsWithNode_SendsTheItemsQueryForTheNode()
    {
        ScriptedXmppConnection connection = Script(false, [Features("PLAIN"), Success, BindFeatures, BindResult, "<iq type='result' id='disco'/>", Close]);

        TransferResult result = await ExecuteAsync(connection, "xmpp://example.com/pubsub.example.com?disco;request=items;node=n%201", Juliet);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual("<iq type=\"get\" id=\"disco\" to=\"pubsub.example.com\" xmlns=\"jabber:client\"><query node=\"n 1\" xmlns=\"http://jabber.org/protocol/disco#items\" /></iq>", connection.WrittenText.ElementAt(4));
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsOffered_UpgradesAndRestartsTheStreamOverTls()
    {
        ScriptedXmppConnection plaintext = ScriptedXmppConnection.Replaying(
            Header + "<stream:features><starttls xmlns='urn:ietf:params:xml:ns:xmpp-tls'><required/></starttls></stream:features>",
            "<proceed xmlns='urn:ietf:params:xml:ns:xmpp-tls'/>");
        ScriptedXmppConnection secured = Script(true, [Features("PLAIN"), Success, BindFeatures, BindResult, Close]);
        ScriptedTlsProvider tls = new(ConnectResult.Connected(secured));

        TransferResult result = await ExecuteAsync(plaintext, "xmpp://example.com/romeo@example.net", Juliet, body: "hi", tls: tls);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { ClientHeader, "<starttls xmlns=\"urn:ietf:params:xml:ns:xmpp-tls\" />" }, plaintext.WrittenText.ToArray());
        Assert.AreEqual(ClientHeader, secured.WrittenText.First());
        Assert.AreSame(plaintext, tls.Plaintext);
        Assert.AreEqual("example.com", tls.TargetHost);
        Assert.IsTrue(secured.IsDisposed);
        Assert.IsTrue(plaintext.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsOfferedOnASecureConnection_DoesNotNegotiateIt()
    {
        ScriptedXmppConnection connection = new([.. new[] { Header + "<stream:features><starttls xmlns='urn:ietf:params:xml:ns:xmpp-tls'/><mechanisms xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><mechanism>PLAIN</mechanism></mechanisms></stream:features>", Success, BindFeatures, BindResult, Close }.Select(Encoding.UTF8.GetBytes)]) { IsSecure = true };

        TransferResult result = await ExecuteAsync(connection, "xmpps://example.com/romeo@example.net", Juliet, body: "hi");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.StartsWith("<auth", connection.WrittenText.ElementAt(1));
    }

    [TestMethod]
    public async Task ExecuteAsync_TlsHandshakeFails_EndsWithItsExitCode()
    {
        ScriptedXmppConnection plaintext = ScriptedXmppConnection.Replaying(Header + "<stream:features><starttls xmlns='urn:ietf:params:xml:ns:xmpp-tls'/></stream:features>", "<proceed xmlns='urn:ietf:params:xml:ns:xmpp-tls'/>");

        TransferResult result = await ExecuteAsync(plaintext, "xmpp://example.com/r@example.net", Juliet, body: "hi", tls: new ScriptedTlsProvider(ConnectResult.Failed(CurlExitCode.SslConnectError, "handshake failed")));

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual("handshake failed", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(Header + "<stream:features><starttls xmlns='urn:ietf:params:xml:ns:xmpp-tls'/></stream:features>|<failure xmlns='urn:ietf:params:xml:ns:xmpp-tls'/>", CurlExitCode.WeirdServerReply, "Unexpected XMPP element <failure>")]
    [DataRow(Header + "<stream:features><mechanisms xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><mechanism>PLAIN</mechanism></mechanisms></stream:features>|<failure xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><not-authorized/></failure>", CurlExitCode.LoginDenied, "XMPP authentication failed: not-authorized")]
    [DataRow(Header + "<stream:features><mechanisms xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><mechanism>PLAIN</mechanism></mechanisms></stream:features>|<foo/>", CurlExitCode.WeirdServerReply, "Unexpected XMPP element <foo>")]
    [DataRow(Header + "<stream:features><mechanisms xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><mechanism>DIGEST-MD5</mechanism></mechanisms></stream:features>", CurlExitCode.LoginDenied, "XMPP server does not offer SASL SCRAM-SHA-256 or SCRAM-SHA-1 or PLAIN")]
    [DataRow(Header + "<stream:features><mechanisms xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><mechanism>PLAIN</mechanism></mechanisms></stream:features>|<challenge xmlns='urn:ietf:params:xml:ns:xmpp-sasl'>=</challenge>", CurlExitCode.WeirdServerReply, "Unexpected XMPP element <challenge>")]
    [DataRow(Header + "<stream:features><mechanisms xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><mechanism>PLAIN</mechanism></mechanisms></stream:features>|<success xmlns='urn:ietf:params:xml:ns:xmpp-sasl'>!!</success>", CurlExitCode.WeirdServerReply, "XMPP server sent malformed XML")]
    [DataRow(Header + "<stream:error><host-unknown xmlns='urn:ietf:params:xml:ns:xmpp-streams'/></stream:error>", CurlExitCode.WeirdServerReply, "XMPP stream error: host-unknown")]
    [DataRow(Header + "<stream:error/>", CurlExitCode.WeirdServerReply, "XMPP stream error: undefined-condition")]
    [DataRow(Header, CurlExitCode.RecvError, "Connection disconnected")]
    [DataRow(Header + "<a></b>|<more/>", CurlExitCode.WeirdServerReply, "XMPP server sent malformed XML")]
    [DataRow(Header + "<stream:features><mechanisms xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><mechanism>PLAIN</mechanism></mechanisms></stream:features>|" + Success + "|" + BindFeatures + "|<iq type='error' id='bind'/>", CurlExitCode.WeirdServerReply, "XMPP stanza error: undefined-condition")]
    [DataRow(Header + "<stream:features><mechanisms xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><mechanism>PLAIN</mechanism></mechanisms></stream:features>|" + Success + "|" + BindFeatures + "|<message/>", CurlExitCode.WeirdServerReply, "Unexpected XMPP element <message>")]
    [DataRow(Header + "<stream:features><mechanisms xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><mechanism>PLAIN</mechanism></mechanisms></stream:features>|" + Success + "|" + BindFeatures + "|" + BindResult + "|<message type='error'><error type='cancel'><service-unavailable xmlns='urn:ietf:params:xml:ns:xmpp-stanzas'/></error></message>", CurlExitCode.WeirdServerReply, "XMPP stanza error: service-unavailable")]
    [DataRow(Header + "<stream:features><mechanisms xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><mechanism>PLAIN</mechanism></mechanisms></stream:features>|" + Success + "|" + BindFeatures + "|" + BindResult + "|<stream:error><conflict xmlns='urn:ietf:params:xml:ns:xmpp-streams'/></stream:error>", CurlExitCode.WeirdServerReply, "XMPP stream error: conflict")]
    public async Task ExecuteAsync_ServerFailure_EndsWithItsExitCodeAndMessage(string served, CurlExitCode exitCode, string message)
    {
        TransferResult result = await ExecuteAsync(ScriptedXmppConnection.Replaying(served.Split('|')), "xmpp://example.com/r@example.net", Juliet, body: "hi");

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ScramSha256Offered_ChoosesItOverScramSha1AndPlainAndSendsRfc7677sProof()
    {
        ScriptedXmppConnection connection = Script(true, [
            Features("PLAIN</mechanism><mechanism>SCRAM-SHA-1</mechanism><mechanism>SCRAM-SHA-256"),
            Sasl("challenge", "r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,s=W22ZaJ0SNY7soEsUEjb6gQ==,i=4096"),
            Sasl("success", "v=6rriTRBi23WpRR/wtup+mMhUZUn/dB5nLTJRsjl95G4="), BindFeatures, BindResult, Close]);

        TransferResult result = await ExecuteScramAsync(connection, "rOprNGfwEbeRWgbNEkqO");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual($"<auth mechanism=\"SCRAM-SHA-256\" xmlns=\"urn:ietf:params:xml:ns:xmpp-sasl\">{Base64("n,,n=user,r=rOprNGfwEbeRWgbNEkqO")}</auth>", connection.WrittenText.ElementAt(1));
        Assert.AreEqual(
            $"<response xmlns=\"urn:ietf:params:xml:ns:xmpp-sasl\">{Base64("c=biws,r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,p=dHzbZapWIk4jUhN+Ute9ytag9zjfMHgsqmmiz7AndVQ=")}</response>",
            connection.WrittenText.ElementAt(2));
    }

    [TestMethod]
    public async Task ExecuteAsync_ScramSha1ServerFinalInAChallenge_AnswersEmptyAndLogsIn()
    {
        ScriptedXmppConnection connection = Script(true, [
            Features("PLAIN</mechanism><mechanism>SCRAM-SHA-1"),
            Sasl("challenge", "r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=4096"),
            Sasl("challenge", "v=rmF9pqV8S7suAoZWja4dJRkFsKQ="),
            "<success xmlns='urn:ietf:params:xml:ns:xmpp-sasl'>=</success>", BindFeatures, BindResult, Close]);

        TransferResult result = await ExecuteScramAsync(connection, "fyko+d2lbbFgONRv9qkxdawL");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(
            $"<response xmlns=\"urn:ietf:params:xml:ns:xmpp-sasl\">{Base64("c=biws,r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,p=v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=")}</response>",
            connection.WrittenText.ElementAt(2));
        Assert.AreEqual("<response xmlns=\"urn:ietf:params:xml:ns:xmpp-sasl\"></response>", connection.WrittenText.ElementAt(3));
    }

    [TestMethod]
    public async Task ExecuteAsync_ScramWrongServerSignature_FailsAuthenticationWithLoginDenied()
    {
        ScriptedXmppConnection connection = Script(true, [
            Features("SCRAM-SHA-1"),
            Sasl("challenge", "r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=4096"),
            Sasl("success", "v=AAAAAAAAAAAAAAAAAAAAAAAAAAA=")]);

        TransferResult result = await ExecuteScramAsync(connection, "fyko+d2lbbFgONRv9qkxdawL");

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual("XMPP authentication failed: server signature mismatch", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoUserAndNoAnonymous_FailsWithLoginDenied()
    {
        TransferResult result = await ExecuteAsync(ScriptedXmppConnection.Replaying(Header + "<stream:features/>"), "xmpp://example.com/", null);

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual("XMPP server does not offer SASL ANONYMOUS", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_DiscoAnswerIsAnError_EndsWithTheCondition()
    {
        ScriptedXmppConnection connection = Script(false, [Features("PLAIN"), Success, BindFeatures, BindResult, "<iq type='error' id='disco'><error type='cancel'><item-not-found xmlns='urn:ietf:params:xml:ns:xmpp-stanzas'/></error></iq>"]);

        TransferResult result = await ExecuteAsync(connection, "xmpp://example.com/nobody@example.com?disco", Juliet);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("XMPP stanza error: item-not-found", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputFails_EndsWithWriteError()
    {
        ScriptedXmppConnection connection = Script(false, [Features("PLAIN"), Success, BindFeatures, BindResult, "<iq type='result' id='disco'/>"]);

        TransferResult result = await ExecuteAsync(connection, "xmpp://example.com", Juliet, new FailingOutput());

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 1 returned 0", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadOrWriteFails_EndsWithRecvOrSendError()
    {
        TransferResult read = await ExecuteAsync(new ScriptedXmppConnection() { FailsReads = true }, "xmpp://example.com", Juliet);
        TransferResult write = await ExecuteAsync(new ScriptedXmppConnection() { FailsWrites = true }, "xmpp://example.com", Juliet);

        Assert.AreEqual(CurlExitCode.RecvError, read.ExitCode);
        Assert.AreEqual(CurlExitCode.SendError, write.ExitCode);
        Assert.AreEqual("Failed sending XMPP data", write.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyWithACharacterXmlForbids_FailsWithBadFunctionArgument()
    {
        ScriptedXmppConnection connection = Script(false, [Features("PLAIN"), Success, BindFeatures, BindResult]);

        TransferResult result = await ExecuteAsync(connection, "xmpp://example.com/r@example.net", Juliet, body: "\u0001");

        Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.ExitCode);
        Assert.AreEqual("XMPP stanza holds a character XML 1.0 forbids", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("xmpp://example.com/?roster", "Unsupported XMPP query type: roster")]
    [DataRow("xmpp://example.com/?disco;request=nodes", "Unsupported XMPP disco request: nodes")]
    public async Task ExecuteAsync_UnsupportedQuery_FailsWithUrlMalformatBeforeConnecting(string url, string message)
    {
        ScriptedXmppConnector connector = new(ConnectResult.Refused("refused"));

        TransferResult result = await new XmppProtocolHandler(connector, new ScriptedTlsProvider(ConnectResult.Refused("x"))).ExecuteAsync(Context(url, Juliet, new MemoryStream(), null));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.IsNull(connector.Target);
    }

    [TestMethod]
    [DataRow("xmpp://example.com/", 5222, false)]
    [DataRow("xmpps://example.com/", 5223, true)]
    [DataRow("XMPPS://example.com:6000/", 6000, true)]
    public async Task ExecuteAsync_Url_ConnectsToItsPortWithTlsForXmpps(string url, int port, bool useTls)
    {
        ScriptedXmppConnector connector = new(ConnectResult.Refused("refused"));

        TransferResult result = await new XmppProtocolHandler(connector, new ScriptedTlsProvider(ConnectResult.Refused("x"))).ExecuteAsync(Context(url, Juliet, new MemoryStream(), null));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
        Assert.AreEqual("example.com", connector.Target!.Host);
        Assert.AreEqual(port, connector.Target.Port);
        Assert.AreEqual(useTls, connector.Target.UseTls);
    }

    [TestMethod]
    public void For_MessageWithoutBody_GivesAMessageWithNoBodyElement() =>
        Assert.AreEqual("<message to=\"r@example.net\" xmlns=\"jabber:client\" />", XmppRequests.For("r@example.net", "message", null).ToString(System.Xml.Linq.SaveOptions.DisableFormatting));

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await new XmppProtocolHandler(new ScriptedXmppConnector(ConnectResult.Refused("x")), new ScriptedTlsProvider(ConnectResult.Refused("x"))).ExecuteAsync(null!));

    private static string Features(string mechanism) =>
        Header + $"<stream:features><mechanisms xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><mechanism>{mechanism}</mechanism></mechanisms></stream:features>";

    private static string Sasl(string element, string text) => $"<{element} xmlns='urn:ietf:params:xml:ns:xmpp-sasl'>{Base64(text)}</{element}>";

    private static string Base64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    private static ValueTask<TransferResult> ExecuteScramAsync(ScriptedXmppConnection connection, string clientNonce) =>
        new XmppProtocolHandler(new ScriptedXmppConnector(ConnectResult.Connected(connection)), new ScriptedTlsProvider(ConnectResult.Refused("no tls")), () => clientNonce)
            .ExecuteAsync(Context("xmpp://example.com/romeo@example.net", new NetworkCredential("user", "pencil"), new MemoryStream(), "hi"));

    private static ScriptedXmppConnection Script(bool failsWhenDrained, string[] chunks) =>
        new([.. chunks.Select(Encoding.UTF8.GetBytes)]) { FailsWhenDrained = failsWhenDrained };

    private static ValueTask<TransferResult> ExecuteAsync(ScriptedXmppConnection connection, string url, NetworkCredential? credentials, Stream? output = null, string? body = null, ScriptedTlsProvider? tls = null) =>
        new XmppProtocolHandler(new ScriptedXmppConnector(ConnectResult.Connected(connection)), tls ?? new ScriptedTlsProvider(ConnectResult.Refused("no tls")))
            .ExecuteAsync(Context(url, credentials, output ?? new MemoryStream(), body));

    private static TransferContext Context(string url, NetworkCredential? credentials, Stream output, string? body) =>
        new() { Url = CurlUrl.Parse(url), Output = output, Credentials = credentials, PostData = body is null ? null : (ReadOnlyMemory<byte>?)Encoding.UTF8.GetBytes(body) };

    private sealed class FailingOutput : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new IOException("full");
    }
}
