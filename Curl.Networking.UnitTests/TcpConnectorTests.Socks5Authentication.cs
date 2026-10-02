using System.Net;
using System.Net.Security;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector" /> through SOCKS5 authentication as <c>--socks5-basic</c>,
/// <c>--socks5-gssapi</c>, <c>--socks5-gssapi-service</c> and <c>--socks5-gssapi-nec</c> shape it
/// (BL-615). The greetings, the refused user and the method-mismatch messages are curl 8.21.0's,
/// measured against a scripted SOCKS5 server (BL-615's Notes); the GSS-API exchange follows
/// RFC 1961 as curl's <c>socks_sspi.c</c> and <c>socks_gssapi.c</c> run it, with a scripted context.
/// </summary>
public sealed partial class TcpConnectorTests
{
    private static readonly byte[] Socks5PicksGssapi = [0x05, 0x01];

    private static readonly byte[] GssapiGrantsNoProtection = [0x01, 0x02, 0x00, 0x02, ScriptedSecurityContextFactory.WrapMarker, 0x00];

    private static readonly byte[] SentConnectRequest = [0x05, 0x01, 0x00, 0x01, 0x7F, 0x00, 0x00, 0x01, 0x1F, 0x90];

    private static readonly Socks5AuthenticationOptions BasicOnly = new(true, false);

    private static readonly Socks5AuthenticationOptions GssapiOnly = new(false, true);

    [TestMethod]
    public void Socks5Authentication_WhenNoneWasGiven_IsCurlsDefault()
    {
        var connector = new TcpConnector(new FakeDnsResolver(), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider());

        Assert.AreSame(Socks5AuthenticationOptions.Default, connector.Socks5Authentication);
    }

    // Greetings

    [TestMethod]
    [DataRow(true, true, false, new byte[] { 0x05, 0x02, 0x00, 0x01 }, DisplayName = "Neither option, no -U: 05 02 00 01")]
    [DataRow(true, true, true, new byte[] { 0x05, 0x03, 0x00, 0x01, 0x02 }, DisplayName = "Neither option, -U u:p: 05 03 00 01 02")]
    [DataRow(true, false, false, new byte[] { 0x05, 0x01, 0x00 }, DisplayName = "--socks5-basic, no -U: 05 01 00")]
    [DataRow(true, false, true, new byte[] { 0x05, 0x02, 0x00, 0x02 }, DisplayName = "--socks5-basic, -U u:p: 05 02 00 02")]
    [DataRow(false, true, false, new byte[] { 0x05, 0x02, 0x00, 0x01 }, DisplayName = "--socks5-gssapi, no -U: 05 02 00 01")]
    [DataRow(false, true, true, new byte[] { 0x05, 0x02, 0x00, 0x01 }, DisplayName = "--socks5-gssapi, -U u:p: 05 02 00 01")]
    public async Task ConnectAsync_ThroughSocks5_OffersTheMethodsTheOptionsAllow(bool allowBasic, bool allowGssapi, bool withCredential, byte[] greeting)
    {
        // Measured: curl -sS [--socks5-basic|--socks5-gssapi] --socks5 127.0.0.1:41615 [-U u:p] http://127.0.0.1:8080/
        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [.. Socks5NoAuthentication, .. Socks5Succeeded],
            withCredential ? new NetworkCredential("u", "p") : null,
            socks5Authentication: new Socks5AuthenticationOptions(allowBasic, allowGssapi));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual((byte[])[.. greeting, .. SentConnectRequest], proxyConnection.Written);
    }

    [TestMethod]
    public async Task ConnectAsync_WithSocks5BasicWhenTheProxyPicksUserNameAndPassword_SendsTheCredential()
    {
        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [0x05, 0x02, 0x01, 0x00, .. Socks5Succeeded],
            new NetworkCredential("u", "p"),
            socks5Authentication: BasicOnly);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            (byte[])[0x05, 0x02, 0x00, 0x02, 0x01, 0x01, (byte)'u', 0x01, (byte)'p', .. SentConnectRequest],
            proxyConnection.Written);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSocks5ProxyRefusesTheUserNameAndPassword_FailsWithTheStatusItSent()
    {
        // Measured: the proxy picked 02 after 05 03 00 01 02, curl sent 01 01 75 01 70, the proxy answered 01 01 ->
        // curl: (97) User was rejected by the SOCKS5 server (1 1).
        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [0x05, 0x02, 0x01, 0x01],
            new NetworkCredential("u", "p"),
            socks5Authentication: Socks5AuthenticationOptions.Default);

        AssertProxyFailure(result, proxyConnection, "User was rejected by the SOCKS5 server (1 1).");
        CollectionAssert.AreEqual(new byte[] { 0x05, 0x03, 0x00, 0x01, 0x02, 0x01, 0x01, 0x75, 0x01, 0x70 }, proxyConnection.Written);
    }

    [TestMethod]
    public async Task ConnectAsync_WithSocks5BasicWhenTheProxyPicksGssapi_FailsAsNotEnabled()
    {
        // Measured: curl -sS --socks5-basic ... sent 05 01 00, the proxy picked 01 ->
        // curl: (97) SOCKS5 GSSAPI per-message authentication is not enabled.
        var (result, proxyConnection) = await ConnectThroughSocksAsync(ProxyKind.Socks5, "127.0.0.1", Socks5PicksGssapi, socks5Authentication: BasicOnly);

        AssertProxyFailure(result, proxyConnection, "SOCKS5 GSSAPI per-message authentication is not enabled.");
        CollectionAssert.AreEqual(new byte[] { 0x05, 0x01, 0x00 }, proxyConnection.Written);
    }

    [TestMethod]
    public async Task ConnectAsync_WithSocks5GssapiWhenTheProxyPicksUserNameAndPassword_FailsAsNotEnabledAndSendsNoCredential()
    {
        // Measured: curl -sS --socks5-gssapi ... -U u:p sent 05 02 00 01, the proxy picked 02 ->
        // curl: (97) BASIC authentication proposed but not enabled.
        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [0x05, 0x02],
            new NetworkCredential("u", "p"),
            socks5Authentication: GssapiOnly);

        AssertProxyFailure(result, proxyConnection, "BASIC authentication proposed but not enabled.");
        CollectionAssert.AreEqual(new byte[] { 0x05, 0x02, 0x00, 0x01 }, proxyConnection.Written);
    }

    // GSS-API (RFC 1961)

    [TestMethod]
    public async Task ConnectAsync_WhenTheSocks5ProxyPicksGssapi_ExchangesTokensThenOffersNoProtectionWrapped()
    {
        var contexts = new ScriptedSecurityContextFactory(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0xA1, 0xA2]),
            new SecurityContextStep(SecurityContextStatus.Completed, [0xA3]));

        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [.. Socks5PicksGssapi, 0x01, 0x01, 0x00, 0x03, 0xB1, 0xB2, 0xB3, .. GssapiGrantsNoProtection, .. Socks5Succeeded, .. BytesAfterTheHandshake],
            socks5Authentication: GssapiOnly with { SecurityContexts = contexts });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            (byte[])[0x05, 0x02, 0x00, 0x01,
                0x01, 0x01, 0x00, 0x02, 0xA1, 0xA2,
                0x01, 0x01, 0x00, 0x01, 0xA3,
                0x01, 0x02, 0x00, 0x02, ScriptedSecurityContextFactory.WrapMarker, 0x00,
                .. SentConnectRequest],
            proxyConnection.Written);
        CollectionAssert.AreEqual(new byte[] { 0xB1, 0xB2, 0xB3 }, contexts.IncomingTokens[1]);
        Assert.IsEmpty(contexts.IncomingTokens[0]);
        Assert.AreEqual((false, true), (contexts.Wrapped[0].Encrypt, contexts.IsDisposed));
        Assert.AreEqual(BytesAfterTheHandshake.Length, proxyConnection.UnreadCount);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheGssapiContextCompletesWithNoToken_SendsNoAuthenticationMessage()
    {
        var contexts = new ScriptedSecurityContextFactory(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0xA1]),
            new SecurityContextStep(SecurityContextStatus.Completed, []));

        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [.. Socks5PicksGssapi, 0x01, 0x01, 0x00, 0x01, 0xB1, .. GssapiGrantsNoProtection, .. Socks5Succeeded],
            socks5Authentication: GssapiOnly with { SecurityContexts = contexts });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            new byte[] { 0x01, 0x01, 0x00, 0x01, 0xA1, 0x01, 0x02, 0x00, 0x02 },
            proxyConnection.Written[4..13]);
    }

    [TestMethod]
    public async Task ConnectAsync_WithGssapiNec_SendsAndReadsTheProtectionLevelUnwrapped()
    {
        var contexts = new ScriptedSecurityContextFactory(new SecurityContextStep(SecurityContextStatus.Completed, [0xA1]));

        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [.. Socks5PicksGssapi, 0x01, 0x02, 0x00, 0x01, 0x00, .. Socks5Succeeded],
            socks5Authentication: GssapiOnly with { SecurityContexts = contexts, GssapiNec = true });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x01, 0x00, 0x01, 0xA1, 0x01, 0x02, 0x00, 0x01, 0x00 }, proxyConnection.Written[4..14]);
        Assert.IsEmpty(contexts.Wrapped);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSocks5ProxyPicksGssapi_AsksForKerberosToRcmdAtTheProxyHost()
    {
        var contexts = new ScriptedSecurityContextFactory(new SecurityContextStep(SecurityContextStatus.Completed, []));

        await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [.. Socks5PicksGssapi, .. GssapiGrantsNoProtection, .. Socks5Succeeded],
            socks5Authentication: Socks5AuthenticationOptions.Default with { SecurityContexts = contexts, GssapiDelegation = SecurityDelegation.Always });

        var request = contexts.Requests.Single();
        Assert.AreEqual(
            (SecurityMechanism.Kerberos, "rcmd", "socks.example", SecurityDelegation.Always, ProtectionLevel.EncryptAndSign),
            (request.Mechanism, request.ServiceName, request.HostName, request.Delegation, request.MessageProtection));
        Assert.IsNull(request.UserName);
    }

    [TestMethod]
    [DataRow(true, Socks5GssapiFailureText.SspiTargetUnknown, DisplayName = "SSPI build (measured)")]
    [DataRow(
        false,
        "GSS-API error: gss_init_sec_context failed: No credentials were supplied, or the credentials were unavailable or inaccessible.\nNo Kerberos credentials available (default cache: FILE:/tmp/krb5cc_1000)",
        DisplayName = "GSS-API build (measured)")]
    public async Task ConnectAsync_WhenTheGssapiContextHasNoCredential_FailsWithThePlatformCurlsText(bool usesSspi, string message)
    {
        // Measured: the proxy picked 01 with no Kerberos ticket at hand; nothing more was sent.
        var contexts = new ScriptedSecurityContextFactory(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));

        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            Socks5PicksGssapi,
            socks5Authentication: GssapiOnly with { SecurityContexts = contexts, UsesSspiTexts = usesSspi, CredentialCacheName = "FILE:/tmp/krb5cc_1000" });

        AssertProxyFailure(result, proxyConnection, message);
        CollectionAssert.AreEqual(new byte[] { 0x05, 0x02, 0x00, 0x01 }, proxyConnection.Written);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSocks5ProxyPicksGssapiAndNoContextFactoryIsGiven_FailsAsWithNoCredential()
    {
        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            Socks5PicksGssapi,
            socks5Authentication: Socks5AuthenticationOptions.Default with { UsesSspiTexts = true });

        AssertProxyFailure(result, proxyConnection, Socks5GssapiFailureText.SspiTargetUnknown);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x01, 0x01, 0x00 }, "Failed to receive SSPI authentication response.", DisplayName = "Header cut short")]
    [DataRow(new byte[] { 0x01, 0xFF, 0x00, 0x00 }, "User was rejected by the SOCKS5 server (1 255).", DisplayName = "Rejected")]
    [DataRow(new byte[] { 0x01, 0x02, 0x00, 0x00 }, "Invalid SSPI authentication response type (1 2).", DisplayName = "Wrong type")]
    [DataRow(new byte[] { 0x01, 0x01, 0x00, 0x02, 0xB1 }, "Failed to receive SSPI authentication token.", DisplayName = "Token cut short")]
    public async Task ConnectAsync_WhenTheProxysAuthenticationAnswerIsWrong_FailsWithProxy(byte[] answer, string message)
    {
        var contexts = new ScriptedSecurityContextFactory(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0xA1]));

        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [.. Socks5PicksGssapi, .. answer],
            socks5Authentication: GssapiOnly with { SecurityContexts = contexts, UsesSspiTexts = true });

        AssertProxyFailure(result, proxyConnection, message);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x01, 0x02, 0x00 }, "Failed to receive GSS-API encryption response.", DisplayName = "Header cut short")]
    [DataRow(new byte[] { 0x01, 0xFF, 0x00, 0x00 }, "User was rejected by the SOCKS5 server (1 255).", DisplayName = "Rejected")]
    [DataRow(new byte[] { 0x01, 0x01, 0x00, 0x00 }, "Invalid GSS-API encryption response type (1 1).", DisplayName = "Wrong type")]
    [DataRow(new byte[] { 0x01, 0x02, 0x00, 0x02, 0x57 }, "Failed to receive GSS-API encryption type.", DisplayName = "Level cut short")]
    [DataRow(new byte[] { 0x01, 0x02, 0x00, 0x01, 0x00 }, "GSS-API error: gss_unwrap failed: A token had an invalid Message Integrity Check (MIC).", DisplayName = "Does not unwrap")]
    [DataRow(new byte[] { 0x01, 0x02, 0x00, 0x03, 0x57, 0x00, 0x00 }, "Invalid GSS-API encryption response length (2).", DisplayName = "Two bytes")]
    [DataRow(new byte[] { 0x01, 0x02, 0x00, 0x02, 0x57, 0x02 }, "SOCKS5 GSS-API protection not yet implemented.", DisplayName = "Confidentiality granted")]
    public async Task ConnectAsync_WhenTheProxysProtectionAnswerIsWrong_FailsWithProxy(byte[] answer, string message)
    {
        var contexts = new ScriptedSecurityContextFactory(new SecurityContextStep(SecurityContextStatus.Completed, []));

        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [.. Socks5PicksGssapi, .. answer],
            socks5Authentication: GssapiOnly with { SecurityContexts = contexts, UsesSspiTexts = false });

        AssertProxyFailure(result, proxyConnection, message);
    }

    [TestMethod]
    [DataRow(true, "SSPI error: EncryptMessage failed: SEC_E_INTERNAL_ERROR (0x80090304) - The Local Security Authority cannot be contacted")]
    [DataRow(false, "GSS-API error: gss_wrap failed: Unspecified GSS failure.  Minor code may provide more information.")]
    public async Task ConnectAsync_WhenTheProtectionLevelCannotBeWrapped_FailsWithoutSendingIt(bool usesSspi, string message)
    {
        var contexts = new ScriptedSecurityContextFactory(new SecurityContextStep(SecurityContextStatus.Completed, [])) { RefusesToWrap = true };

        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            Socks5PicksGssapi,
            socks5Authentication: GssapiOnly with { SecurityContexts = contexts, UsesSspiTexts = usesSspi });

        AssertProxyFailure(result, proxyConnection, message);
        Assert.HasCount(4, proxyConnection.Written);
    }

    // The -v lines

    [TestMethod]
    [DataRow(true, new[] { Socks5GssapiFailureText.SspiTargetUnknown, "Failed to initialize security context.", "Unable to negotiate SOCKS5 GSS-API context." }, DisplayName = "SSPI build (measured)")]
    [DataRow(
        false,
        new[]
        {
            "GSS-API error: gss_init_sec_context failed: No credentials were supplied, or the credentials were unavailable or inaccessible.\nNo Kerberos credentials available (default cache: FILE:/tmp/krb5cc_1000)",
            "Failed to initial GSS-API token.",
            "Unable to negotiate SOCKS5 GSS-API context.",
        },
        DisplayName = "GSS-API build (measured)")]
    public async Task ConnectAsync_WhenTheGssapiContextHasNoCredential_ReportsThePlatformCurlsVerboseLinesInOrder(bool usesSspi, string[] lines)
    {
        // Measured (BL-615's Notes): curl -v --socks5-gssapi, the proxy picking 01 with no Kerberos ticket at hand.
        var contexts = new ScriptedSecurityContextFactory(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));
        var events = new RecordingTransferEvents();

        await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            Socks5PicksGssapi,
            socks5Authentication: GssapiOnly with { SecurityContexts = contexts, UsesSspiTexts = usesSspi, CredentialCacheName = "FILE:/tmp/krb5cc_1000" },
            events: events);

        CollectionAssert.AreEqual(lines, events.Info[^lines.Length..]);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSocks5ProxyPicksGssapiAndNoContextFactoryIsGiven_ReportsTheContextFailureLines()
    {
        var events = new RecordingTransferEvents();

        await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            Socks5PicksGssapi,
            socks5Authentication: Socks5AuthenticationOptions.Default with { UsesSspiTexts = false, CredentialCacheName = "FILE:/tmp/krb5cc_0" },
            events: events);

        CollectionAssert.AreEqual(
            new[]
            {
                "GSS-API error: gss_init_sec_context failed: No credentials were supplied, or the credentials were unavailable or inaccessible.\nNo Kerberos credentials available (default cache: FILE:/tmp/krb5cc_0)",
                "Failed to initial GSS-API token.",
                "Unable to negotiate SOCKS5 GSS-API context.",
            },
            events.Info[^3..]);
    }

    [TestMethod]
    [DataRow(true, "Invalid SSPI encryption response type (1 1).", DisplayName = "SSPI build")]
    [DataRow(false, "Invalid GSS-API encryption response type (1 1).", DisplayName = "GSS-API build")]
    public async Task ConnectAsync_WhenTheGssapiNegotiationFailsAfterTheContext_ReportsItsMessageThenUnableToNegotiate(bool usesSspi, string message)
    {
        // curl's socks.c follows every failed negotiation with this failf; the failure's own failf comes first.
        var contexts = new ScriptedSecurityContextFactory(new SecurityContextStep(SecurityContextStatus.Completed, []));
        var events = new RecordingTransferEvents();

        await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [.. Socks5PicksGssapi, 0x01, 0x01, 0x00, 0x00],
            socks5Authentication: GssapiOnly with { SecurityContexts = contexts, UsesSspiTexts = usesSspi, GssapiNec = true },
            events: events);

        CollectionAssert.AreEqual(new[] { message, "Unable to negotiate SOCKS5 GSS-API context." }, events.Info[^2..]);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheGssapiNegotiationSucceeds_ReportsNoNegotiationLine()
    {
        var contexts = new ScriptedSecurityContextFactory(new SecurityContextStep(SecurityContextStatus.Completed, []));
        var events = new RecordingTransferEvents();

        await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [.. Socks5PicksGssapi, .. GssapiGrantsNoProtection, .. Socks5Succeeded],
            socks5Authentication: GssapiOnly with { SecurityContexts = contexts },
            events: events);

        Assert.IsFalse(events.Info.Any(line => line.Contains("GSS-API context", StringComparison.Ordinal)));
    }
}
