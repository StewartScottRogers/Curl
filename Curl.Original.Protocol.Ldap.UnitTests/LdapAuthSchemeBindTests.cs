using System.Net;
using Curl.Original.Protocol.Ldap.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Pins the bind each auth mask chooses with <c>-u</c> (ADR-1238, BL-4899) against curl 8.22.0
/// with WinLDAP on Windows, recorded with <c>Record-CurlExchange.ps1 -Script</c> on 2026-11-11
/// against a server that answers every search with an empty SearchResultDone and every bind with
/// success: <c>--ntlm</c> reads <c>supportedCapabilities</c> then sends the Sicily NTLM bind,
/// <c>--negotiate</c> makes the logon bind's rootDSE reads then the Sicily NTLM bind,
/// <c>--digest</c> reads <c>supportedCapabilities</c> and <c>supportedSASLMechanisms</c> then sends
/// the SASL <c>DIGEST-MD5</c> bind; each attempt is made twice, then an UnbindRequest, and exit 38
/// <c>Protocol Error</c>. Only a mask holding Basic sends the simple bind with the password, and the
/// OpenLDAP build always does. Tokens are short stand-ins from a fake security package.
/// </summary>
[TestClass]
public sealed class LdapAuthSchemeBindTests
{
    private const string NtlmNegotiate = "4e 54 4c 4d 53 53 50 00 01 00 00 00";

    private const string User = "cn=admin,dc=x";

    private const string Password = "pw";

    /// <summary>The simple bind curl 8.22.0 sent for <c>-u cn=admin,dc=x:pw</c>, the password in cleartext.</summary>
    private const string WinLdapSimpleBind =
        "30 84 00 00 00 1f 02 01 01 60 84 00 00 00 16 02 01 03 04 0d 63 6e 3d 61 64 6d 69 6e 2c 64 63 3d 78 80 02 70 77";

    [TestMethod]
    public async Task ExecuteAsync_WinLdapNtlm_ReadsTheCapabilitiesThenSendsTheSicilyBindNeverThePassword()
    {
        var tokens = new FakeLogonTokenSource(2, NtlmNegotiate);

        (TransferResult result, byte[] sent) = await RunAsync(LdapDialect.WinLdap, HttpAuthSchemes.Ntlm | HttpAuthSchemes.Digest, tokens, Done(1), BindReply(2, 0), Done(3), BindReply(4, 0));

        Assert.AreEqual(LdapBind.WinLdapBindFailed(WinLdapResultText.ProtocolError), result);
        CollectionAssert.AreEqual(Hex.Bytes(Join(CapabilitiesSearch(1), SicilyNegotiateBind(2), CapabilitiesSearch(3), SicilyNegotiateBind(4), Unbind(5))), sent);
        CollectionAssert.AreEqual(new[] { LdapLogonPackage.Ntlm, LdapLogonPackage.Ntlm }, tokens.Starts.Select(start => start.Package).ToArray());
        Assert.AreEqual(User, tokens.Credentials[0].UserName);
        Assert.AreEqual(Password, tokens.Credentials[0].Password);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapNegotiate_MakesTheLogonBindsReadsWithTheUsersCredentials()
    {
        var tokens = new FakeLogonTokenSource(2, NtlmNegotiate);

        (TransferResult result, byte[] sent) = await RunAsync(
            LdapDialect.WinLdap,
            HttpAuthSchemes.Negotiate | HttpAuthSchemes.Ntlm,
            tokens,
            Done(1),
            Done(2),
            Done(3),
            BindReply(4, 0),
            Done(5),
            Done(6),
            Done(7),
            BindReply(8, 0));

        Assert.AreEqual(LdapBind.WinLdapBindFailed(WinLdapResultText.ProtocolError), result);
        CollectionAssert.AreEqual(
            Hex.Bytes(Join(CapabilitiesSearch(1), MechanismsSearch(2), CapabilitiesSearch(3), SicilyNegotiateBind(4), CapabilitiesSearch(5), MechanismsSearch(6), CapabilitiesSearch(7), SicilyNegotiateBind(8), Unbind(9))),
            sent);
        Assert.AreEqual(User, tokens.Credentials[1].UserName);
        Assert.AreEqual(Password, tokens.Credentials[1].Password);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapDigestAnsweredSuccess_SendsTheDigestMd5BindAndFailsWithProtocolError()
    {
        var tokens = new FakeLogonTokenSource(2, NtlmNegotiate);

        (TransferResult result, byte[] sent) = await RunAsync(LdapDialect.WinLdap, HttpAuthSchemes.Digest, tokens, Done(1), Done(2), BindReply(3, 0), Done(4), Done(5), BindReply(6, 0));

        Assert.AreEqual(LdapBind.WinLdapBindFailed(WinLdapResultText.ProtocolError), result);
        CollectionAssert.AreEqual(Hex.Bytes(Join(CapabilitiesSearch(1), MechanismsSearch(2), DigestBind(3), CapabilitiesSearch(4), MechanismsSearch(5), DigestBind(6), Unbind(7))), sent);
        Assert.IsEmpty(tokens.Starts);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapDigestChallenged_FailsWithLocalError()
    {
        (TransferResult result, _) = await RunAsync(LdapDialect.WinLdap, HttpAuthSchemes.Digest, new FakeLogonTokenSource(0), Done(1), Done(2), BindReply(3, 14), Done(4), Done(5), BindReply(6, 14));

        Assert.AreEqual(LdapBind.WinLdapBindFailed(WinLdapResultText.LocalError), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapDigestRefused_FailsWithTheServersResultCode()
    {
        (TransferResult result, _) = await RunAsync(LdapDialect.WinLdap, HttpAuthSchemes.Digest, new FakeLogonTokenSource(0), Done(1), Done(2), BindReply(3, 49), Done(4), Done(5), BindReply(6, 49));

        Assert.AreEqual(LdapBind.WinLdapBindFailed(49), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapDigestServerClosedAfterTheCapabilities_FailsWithTimeout()
    {
        (TransferResult result, byte[] sent) = await RunAsync(LdapDialect.WinLdap, HttpAuthSchemes.Digest, new FakeLogonTokenSource(0), Done(1));

        Assert.AreEqual(LdapBind.WinLdapBindFailed(WinLdapResultText.Timeout), result);
        CollectionAssert.AreEqual(Hex.Bytes(Join(CapabilitiesSearch(1), MechanismsSearch(2))), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapNtlmServerClosed_FailsWithTimeout()
    {
        (TransferResult result, byte[] sent) = await RunAsync(LdapDialect.WinLdap, HttpAuthSchemes.Ntlm, new FakeLogonTokenSource(0));

        Assert.AreEqual(LdapBind.WinLdapBindFailed(WinLdapResultText.Timeout), result);
        CollectionAssert.AreEqual(Hex.Bytes(CapabilitiesSearch(1)), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapMaskWithoutBasicNegotiateNtlmOrDigest_BindsAsTheLoggedOnUser()
    {
        var tokens = new FakeLogonTokenSource(2, NtlmNegotiate);

        (_, byte[] sent) = await RunAsync(LdapDialect.WinLdap, HttpAuthSchemes.None, tokens, Done(1), Done(2), Done(3), BindReply(4, 0));

        Assert.AreSame(CredentialCache.DefaultNetworkCredentials, tokens.Credentials[0]);
        CollectionAssert.AreEqual(Hex.Bytes(Join(CapabilitiesSearch(1), MechanismsSearch(2), CapabilitiesSearch(3), SicilyNegotiateBind(4))), sent[..Hex.Bytes(Join(CapabilitiesSearch(1), MechanismsSearch(2), CapabilitiesSearch(3), SicilyNegotiateBind(4))).Length]);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapMaskWithBasicAndNtlm_SendsTheSimpleBind()
    {
        var tokens = new FakeLogonTokenSource(0);

        (TransferResult result, byte[] sent) = await RunAsync(LdapDialect.WinLdap, HttpAuthSchemes.Basic | HttpAuthSchemes.Ntlm, tokens, BindReply(1, 0), Done(2));

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(Hex.Bytes(WinLdapSimpleBind), sent[..Hex.Bytes(WinLdapSimpleBind).Length]);
        Assert.IsEmpty(tokens.Starts);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapWithoutAuthOption_SendsTheSimpleBind()
    {
        var tokens = new FakeLogonTokenSource(0);

        (TransferResult result, byte[] sent) = await RunAsync(LdapDialect.WinLdap, null, tokens, BindReply(1, 0), Done(2));

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(Hex.Bytes(WinLdapSimpleBind), sent[..Hex.Bytes(WinLdapSimpleBind).Length]);
        Assert.IsEmpty(tokens.Starts);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapNtlm_SendsTheSimpleBindAsTheOpenLdapBuildDoes()
    {
        var tokens = new FakeLogonTokenSource(0);

        (TransferResult result, _) = await RunAsync(LdapDialect.OpenLdap, HttpAuthSchemes.Ntlm, tokens, BindReply(1, 0), Done(2));

        Assert.AreEqual(TransferResult.Success(0), result);
        Assert.IsEmpty(tokens.Starts);
    }

    private static async Task<(TransferResult Result, byte[] Sent)> RunAsync(LdapDialect dialect, HttpAuthSchemes? schemes, FakeLogonTokenSource tokens, params string[] replies)
    {
        var connection = new ScriptedConnection([.. replies.Select(Hex.Bytes)]);
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), dialect, tokens);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ldap://127.0.0.1:18500/dc=x"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential(User, Password),
            Http = schemes is { } mask ? new Curl.Protocol.Abstractions.HttpRequestOptions { AuthSchemes = mask } : null,
        };

        TransferResult result = await handler.ExecuteAsync(context);

        return (result, connection.Sent);
    }

    private static string Join(params string[] messages) => string.Join(' ', messages);

    private static string CapabilitiesSearch(int id) =>
        $"30 84 00 00 00 44 02 01 {id:x2} 63 84 00 00 00 3b 04 00 0a 01 00 0a 01 00 02 01 00 02 01 78 01 01 00 87 0b 6f 62 6a 65 63 74 63 6c 61 73 73 30 84 00 00 00 17 04 15 73 75 70 70 6f 72 74 65 64 43 61 70 61 62 69 6c 69 74 69 65 73";

    private static string MechanismsSearch(int id) =>
        $"30 84 00 00 00 46 02 01 {id:x2} 63 84 00 00 00 3d 04 00 0a 01 00 0a 01 00 02 01 00 02 01 78 01 01 00 87 0b 6f 62 6a 65 63 74 63 6c 61 73 73 30 84 00 00 00 19 04 17 73 75 70 70 6f 72 74 65 64 53 41 53 4c 4d 65 63 68 61 6e 69 73 6d 73";

    private static string SicilyNegotiateBind(int id) =>
        $"30 84 00 00 00 20 02 01 {id:x2} 60 84 00 00 00 17 02 01 03 04 04 4e 54 4c 4d 8a 0c {NtlmNegotiate}";

    /// <summary>The SASL <c>DIGEST-MD5</c> bind with empty credentials, byte for byte as curl 8.22.0 sent it.</summary>
    private static string DigestBind(int id) =>
        $"30 84 00 00 00 22 02 01 {id:x2} 60 84 00 00 00 19 02 01 03 04 00 a3 84 00 00 00 0e 04 0a 44 49 47 45 53 54 2d 4d 44 35 04 00";

    private static string Unbind(int id) => $"30 84 00 00 00 05 02 01 {id:x2} 42 00";

    private static string Done(int id) => $"30 0c 02 01 {id:x2} 65 07 0a 01 00 04 00 04 00";

    private static string BindReply(int id, int resultCode) => $"30 0c 02 01 {id:x2} 61 07 0a 01 {resultCode:x2} 04 00 04 00";
}
