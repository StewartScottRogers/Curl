using Curl.Core;
using Curl.Extension.Composition;
using Curl.Extension.WebTransport.Session;
using Curl.Extension.WebTransport.Wire;
using Curl.Http3;
using Curl.Protocol.Abstractions;
using Curl.Quic;

namespace Curl.Extension.WebTransport;

/// <summary>Checks the extension's description, factory and registration (ADR-0999).</summary>
[TestClass]
public sealed class WebTransportExtensionTests
{
    private static readonly ProtocolHandlerServices Services = new(null!, new FakeDatagramConnector(null), null!, null!);

    [TestMethod]
    public void Description_NamesTheSchemeAndTheDraft()
    {
        CollectionAssert.AreEqual(new[] { "webtransport" }, WebTransportExtension.Description.Schemes.ToArray());
        Assert.AreEqual("https://datatracker.ietf.org/doc/html/draft-ietf-webtrans-http3-16", WebTransportExtension.Description.Specification.ToString());
    }

    [TestMethod]
    public void CreateHandler_GivesTheWebTransportHandler() =>
        Assert.IsInstanceOfType<WebTransportProtocolHandler>(WebTransportExtension.CreateHandler(Services));

    [TestMethod]
    public void CreateHandler_WithoutServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => WebTransportExtension.CreateHandler(null!));

    [TestMethod]
    public void ExtensionProtocolHandlers_WithExtensionsOn_ServeWebTransport() =>
        Assert.IsTrue(new ProtocolDispatcher(ExtensionProtocolHandlers.For(_ => null)!(Services)).Serves("webtransport"));

    [TestMethod]
    public async Task Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme()
    {
        Assert.IsNull(ExtensionProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null));
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse("webtransport://example.com/echo"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("Protocol \"webtransport\" not supported", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ChannelDoesNotOpen_FailsWithTheConnectorsExitCode()
    {
        TransferResult result = await WebTransportExtension.CreateHandler(Services).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("webtransport://example.com/"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Could not connect to server", result.ErrorMessage);
    }
}

/// <summary>Checks <see cref="WebTransportWire" /> against draft-ietf-webtrans-http3-16 sections 3.1, 4.2 and 4.3.</summary>
[TestClass]
public sealed class WebTransportWireTests
{
    [TestMethod]
    public void StreamHeaderAndData_Section4_2_IsType0x54ThenTheSessionIdThenTheData() =>
        CollectionAssert.AreEqual(new byte[] { 0x40, 0x54, 0x40, 0x40, 0x61 }, WebTransportWire.StreamHeaderAndData(0x54, 64, "a"u8));

    [TestMethod]
    public void StreamHeaderAndData_Section4_3_IsSignal0x41ThenTheSessionId() =>
        CollectionAssert.AreEqual(new byte[] { 0x40, 0x41, 0x04 }, WebTransportWire.StreamHeaderAndData(0x41, 4, []));

    [TestMethod]
    public void ClientSettings_Section3_1_AddH3DatagramAndWtEnabledToCurlsSettings()
    {
        CollectionAssert.IsSubsetOf(Http3LocalUnidirectionalStreams.CurlSettings.ToArray(), WebTransportWire.ClientSettings.ToArray());
        CollectionAssert.Contains(WebTransportWire.ClientSettings.ToArray(), new Http3Setting(0x33, 1));
        CollectionAssert.Contains(WebTransportWire.ClientSettings.ToArray(), new Http3Setting(0x2c7cf000, 1));
    }

    [TestMethod]
    public void AllowsSessions_Section3_1_WtEnabledAbove1_DoesNotAllowSessions() =>
        Assert.IsFalse(WebTransportWire.AllowsSessions(new Http3SettingsFrame([new(0x08, 1), new(0x33, 1), new(0x2c7cf000, 2)])));

    [TestMethod]
    public void CloseSession_Section6_IsCapsule0x2843WithTheErrorCodeAndMessage() =>
        CollectionAssert.AreEqual(new byte[] { 0x68, 0x43, 0x06, 0x00, 0x00, 0x01, 0x02, 0x6f, 0x6b }, new WebTransportCloseSession(0x0102, "ok").ToCapsuleBytes());

    [TestMethod]
    public void CloseSession_Section6_MessageOver1024Bytes_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => new WebTransportCloseSession(0, new string('x', 1025)).ToCapsuleBytes());
}

/// <summary>Checks how <see cref="WebTransportRequest" /> reads a <c>webtransport://</c> URL (ADR-1077).</summary>
[TestClass]
public sealed class WebTransportRequestTests
{
    [TestMethod]
    public void Of_NoPort_DialsUdp443AndSendsTheHostAlone()
    {
        WebTransportRequest request = WebTransportRequest.Of(CurlUrl.Parse("webtransport://example.com/echo"), null);

        Assert.AreEqual(("example.com", 443, "example.com", "/echo"), (request.Host, request.Port, request.Authority, request.Path));
        Assert.AreEqual(WebTransportMode.Bidirectional, request.Mode);
        Assert.IsTrue(request.Data.IsEmpty);
    }

    [TestMethod]
    [DataRow("webtransport://h:8443/p?wt=uni", "/p", (int)WebTransportMode.Unidirectional)]
    [DataRow("webtransport://h:8443/p?a=1&wt=datagram&b=2", "/p?a=1&b=2", (int)WebTransportMode.Datagram)]
    [DataRow("webtransport://h:8443/p?", "/p", (int)WebTransportMode.Bidirectional)]
    [DataRow("webtransport://h:8443/p?wt=bidi", "/p", (int)WebTransportMode.Bidirectional)]
    public void Of_WtField_ChoosesTheModeAndIsNotSent(string url, string path, int mode)
    {
        WebTransportRequest request = WebTransportRequest.Of(CurlUrl.Parse(url), "d"u8.ToArray());

        Assert.AreEqual(("h:8443", 8443, path, (WebTransportMode)mode), (request.Authority, request.Port, request.Path, request.Mode));
        Assert.AreEqual(1, request.Data.Length);
    }
}

/// <summary>Checks <see cref="WebTransportQuicDialer" /> over an in-memory QUIC server (section 3.1, RFC 9221 section 3).</summary>
[TestClass]
public sealed class WebTransportQuicDialerTests
{
    private const int HangTimeoutMilliseconds = 120_000;

    private static readonly WebTransportRequest Request = new("wt.example", 4433, "wt.example:4433", "/", WebTransportMode.Bidirectional, ReadOnlyMemory<byte>.Empty);

    [TestMethod]
    public void CreateQuicSettings_Section3_1_OffersH3AndAdvertisesDatagrams()
    {
        QuicClientSettings settings = WebTransportQuicDialer.CreateQuicSettings("wt.example");

        CollectionAssert.AreEqual(new[] { "h3" }, settings.Tls.ApplicationProtocols.ToArray());
        Assert.AreEqual("wt.example", settings.Tls.ServerName);
        Assert.AreEqual(65535UL, settings.TransportParameters.MaxDatagramFrameSize);
    }

    [TestMethod]
    public async Task DialAsync_ChannelDoesNotOpen_ThrowsTheConnectorsExitCode()
    {
        WebTransportException failure = await Assert.ThrowsExactlyAsync<WebTransportException>(async () => await Dialer(new FakeDatagramConnector(null)).DialAsync(Request, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.CouldntConnect, failure.ExitCode);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task DialAsync_HandshakeFails_ThrowsItsExitCodeAndDisposesTheChannel()
    {
        using QuicTestServer server = new() { CloseAfterClientHello = 0x0a };
        QuicTestChannel channel = new(server);

        WebTransportException failure = await Assert.ThrowsExactlyAsync<WebTransportException>(async () => await Dialer(new FakeDatagramConnector(channel)).DialAsync(Request, CancellationToken.None));

        Assert.AreNotEqual(CurlExitCode.Ok, failure.ExitCode);
        Assert.IsTrue(channel.IsDisposed);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task DialAsync_RFC9221Section3_GivesAConnectionCarryingTheServersDatagrams()
    {
        using QuicTestServer server = new() { ConfigureTransportParameters = parameters => parameters with { MaxDatagramFrameSize = 1200 } };
        FakeDatagramConnector connector = new(new QuicTestChannel(server));

        await using IMultiplexedConnection connection = await Dialer(connector).DialAsync(Request, CancellationToken.None);

        Assert.AreEqual(("wt.example", 4433), connector.Opens.Single());
        Assert.AreEqual("h3", connection.ApplicationProtocol);
        Assert.IsGreaterThan(0, ((IDatagramCarryingConnection)connection).MaxDatagramPayloadLength);
    }

    private static WebTransportQuicDialer Dialer(FakeDatagramConnector connector) =>
        new(connector, _ => new QuicTestVerifier(), TimeProvider.System);
}
