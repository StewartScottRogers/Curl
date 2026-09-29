using Curl.Protocol.Abstractions;
using Curl.Protocol.Ldap.Fakes;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Pins the WinLDAP dialect's bind without <c>-u</c> against curl 8.21.0 with WinLDAP on
/// Windows, recorded with <c>Record-CurlExchange.ps1 -Script</c> on 2026-09-28 (BL-830): the
/// rootDSE reads, the <c>GSS-SPNEGO</c> and Sicily NTLM binds, curl's retry of a failed
/// attempt, and each failure's exit code and message. The NTLM negotiate message is the one
/// WinLDAP sent; later tokens are short stand-ins from a fake security package.
/// </summary>
public sealed partial class LdapProtocolHandlerTests
{
    /// <summary>The NTLM negotiate message WinLDAP sent in both binds.</summary>
    private const string NtlmNegotiate = "4e 54 4c 4d 53 53 50 00 01 00 00 00 b7 b2 08 e2 09 00 09 00 37 00 00 00 0f 00 0f 00 28 00 00 00 0a 00 f4 65 00 00 00 0f 53 54 45 57 41 52 54 2d 52 4f 47 45 52 53 2d 57 4f 52 4b 47 52 4f 55 50";

    private const string NtlmChallenge = "4e 54 4c 4d 53 53 50 00 02 00 00 00";

    private const string NtlmAuthenticate = "4e 54 4c 4d 53 53 50 00 03 00 00 00";

    private const string NotALdapMessage = "04 00";

    [TestMethod]
    public void Constructor_NullLogonTokenSource_Throws()
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));

        Assert.ThrowsExactly<ArgumentNullException>(() => new LdapProtocolHandler(connector, LdapDialect.WinLdap, null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapWithoutUserOfferedGssSpnego_ReadsTheRootDseThenBindsWithSpnegoAndSearches()
    {
        var tokens = new FakeLogonTokenSource(2, NtlmNegotiate, NtlmAuthenticate);

        (TransferResult result, byte[] sent) = await RunLogonAsync(
            tokens,
            CapabilitiesEntry(1),
            Done(1),
            MechanismsEntry(2),
            Done(2),
            SpnegoChallenge(3),
            BindReply(4, 0),
            Sealed(0, Done(5)));

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(Hex.Bytes(Join(CapabilitiesSearch(1), MechanismsSearch(2), SpnegoBind(3), SpnegoAuthenticateBind(4), Sealed(0, Search(5)), Sealed(1, Unbind(6)))), sent);
        CollectionAssert.AreEqual(new[] { (LdapLogonPackage.Negotiate, "ldap/127.0.0.1") }, tokens.Starts.ToArray());
        CollectionAssert.AreEqual(Array.Empty<byte>(), tokens.Challenges[0]);
        CollectionAssert.AreEqual(Hex.Bytes(NtlmChallenge), tokens.Challenges[1]);
        Assert.AreEqual(1, tokens.Disposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapWithoutUserNotOfferedGssSpnego_ReadsTheCapabilitiesAgainThenBindsWithSicily()
    {
        var tokens = new FakeLogonTokenSource(2, NtlmNegotiate, NtlmAuthenticate);

        (TransferResult result, byte[] sent) = await RunLogonAsync(
            tokens,
            Done(1),
            Done(2),
            Done(3),
            SicilyChallenge(4),
            BindReply(5, 0),
            Sealed(0, Done(6)));

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(Hex.Bytes(Join(CapabilitiesSearch(1), MechanismsSearch(2), CapabilitiesSearch(3), SicilyNegotiateBind(4), SicilyResponseBind(5), Sealed(0, Search(6)), Sealed(1, Unbind(7)))), sent);
        CollectionAssert.AreEqual(new[] { (LdapLogonPackage.Ntlm, "ldap/127.0.0.1") }, tokens.Starts.ToArray());
        CollectionAssert.AreEqual(Hex.Bytes(NtlmChallenge), tokens.Challenges[1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapRootDseRepliesOtherThanEntriesAndDone_AreSkipped()
    {
        (TransferResult result, byte[] sent) = await RunLogonAsync(
            new FakeLogonTokenSource(2, NtlmNegotiate, NtlmAuthenticate),
            "30 05 02 01 01 04 00",
            Done(7),
            Done(1),
            "30 10 02 01 02 64 0b 04 03 6e 3d 78 30 04 30 02 04 00",
            MechanismsEntry(2),
            Done(2),
            SpnegoChallenge(3),
            BindReply(4, 0),
            Sealed(0, Done(5)));

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(Hex.Bytes(Join(CapabilitiesSearch(1), MechanismsSearch(2), SpnegoBind(3), SpnegoAuthenticateBind(4), Sealed(0, Search(5)), Sealed(1, Unbind(6)))), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapSpnegoBindRefusedTwice_RetriesWithSpnegoThenUnbindsAndFailsWithTheSecondsText()
    {
        (TransferResult result, byte[] sent) = await RunLogonAsync(
            new FakeLogonTokenSource(2, NtlmNegotiate, NtlmAuthenticate),
            CapabilitiesEntry(1),
            Done(1),
            MechanismsEntry(2),
            Done(2),
            BindReply(3, 0x31),
            Done(4),
            Done(5),
            BindReply(6, 0x35));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Unwilling To Perform"), result);
        CollectionAssert.AreEqual(Hex.Bytes(Join(CapabilitiesSearch(1), MechanismsSearch(2), SpnegoBind(3), CapabilitiesSearch(4), MechanismsSearch(5), SpnegoBind(6), Unbind(7))), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapSicilyNegotiateRefusedTwice_UnbindsAndFailsWith38InvalidCredentials()
    {
        (TransferResult result, byte[] sent) = await RunLogonAsync(
            new FakeLogonTokenSource(2, NtlmNegotiate, NtlmAuthenticate),
            Done(1),
            Done(2),
            Done(3),
            BindReply(4, 0x31),
            Done(5),
            Done(6),
            Done(7),
            BindReply(8, 0x31));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Invalid Credentials"), result);
        CollectionAssert.AreEqual(
            Hex.Bytes(Join(CapabilitiesSearch(1), MechanismsSearch(2), CapabilitiesSearch(3), SicilyNegotiateBind(4), CapabilitiesSearch(5), MechanismsSearch(6), CapabilitiesSearch(7), SicilyNegotiateBind(8), Unbind(9))),
            sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapSicilyResponseRefusedTwice_UnbindsAndFailsWithTheSecondsText()
    {
        (TransferResult result, byte[] sent) = await RunLogonAsync(
            new FakeLogonTokenSource(2, NtlmNegotiate, NtlmAuthenticate),
            Done(1),
            Done(2),
            Done(3),
            SicilyChallenge(4),
            BindReply(5, 0x31),
            Done(6),
            Done(7),
            Done(8),
            SicilyChallenge(9),
            BindReply(10, 0x31));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Invalid Credentials"), result);
        CollectionAssert.AreEqual(Hex.Bytes(Unbind(11)), sent[^Hex.Bytes(Unbind(11)).Length..]);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public async Task ExecuteAsync_WinLdapServerClosesWhileTheFirstAttemptReadsTheRootDse_FailsWith38Timeout(int answered)
    {
        string[] replies = [.. new[] { Done(1), Done(2) }.Take(answered)];

        (TransferResult result, byte[] sent) = await RunLogonAsync(new FakeLogonTokenSource(2, NtlmNegotiate, NtlmAuthenticate), replies);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Timeout"), result);
        CollectionAssert.AreEqual(Hex.Bytes(Join([.. new[] { CapabilitiesSearch(1), MechanismsSearch(2), CapabilitiesSearch(3) }.Take(answered + 1)])), sent);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(NotABindResponse)]
    [DataRow(NotALdapMessage)]
    public async Task ExecuteAsync_WinLdapSpnegoBindUnanswered_FailsWith38TimeoutWithoutUnbinding(string? reply)
    {
        string[] replies = [CapabilitiesEntry(1), Done(1), MechanismsEntry(2), Done(2), .. reply is null ? Array.Empty<string>() : new[] { reply.Replace("02 01 01", "02 01 03", StringComparison.Ordinal) }];

        (TransferResult result, byte[] sent) = await RunLogonAsync(new FakeLogonTokenSource(2, NtlmNegotiate, NtlmAuthenticate), replies);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Timeout"), result);
        CollectionAssert.AreEqual(Hex.Bytes(Join(CapabilitiesSearch(1), MechanismsSearch(2), SpnegoBind(3))), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapServerClosesWhileTheRetryReadsTheRootDse_FailsWith38ServerDown()
    {
        (TransferResult result, byte[] sent) = await RunLogonAsync(
            new FakeLogonTokenSource(2, NtlmNegotiate, NtlmAuthenticate),
            CapabilitiesEntry(1),
            Done(1),
            MechanismsEntry(2),
            Done(2),
            BindReply(3, 0x31));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Server Down"), result);
        CollectionAssert.AreEqual(Hex.Bytes(Join(CapabilitiesSearch(1), MechanismsSearch(2), SpnegoBind(3), CapabilitiesSearch(4))), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapServerClosesOnTheRetrysBind_FailsWith38Timeout()
    {
        (TransferResult result, byte[] sent) = await RunLogonAsync(
            new FakeLogonTokenSource(2, NtlmNegotiate, NtlmAuthenticate),
            CapabilitiesEntry(1),
            Done(1),
            MechanismsEntry(2),
            Done(2),
            BindReply(3, 0x31),
            Done(4),
            Done(5));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Timeout"), result);
        CollectionAssert.AreEqual(Hex.Bytes(Join(CapabilitiesSearch(1), MechanismsSearch(2), SpnegoBind(3), CapabilitiesSearch(4), MechanismsSearch(5), SpnegoBind(6))), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapPackageProducesNoFirstToken_FailsBothAttemptsWithLocalError()
    {
        (TransferResult result, byte[] sent) = await RunLogonAsync(
            new FakeLogonTokenSource(2, [null]),
            Done(1),
            Done(2),
            Done(3),
            Done(4),
            Done(5),
            Done(6));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Local Error"), result);
        CollectionAssert.AreEqual(
            Hex.Bytes(Join(CapabilitiesSearch(1), MechanismsSearch(2), CapabilitiesSearch(3), CapabilitiesSearch(4), MechanismsSearch(5), CapabilitiesSearch(6), Unbind(7))),
            sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapSpnegoPackageProducesNoFirstToken_SendsNoBind()
    {
        (TransferResult result, byte[] sent) = await RunLogonAsync(
            new FakeLogonTokenSource(2, [null]),
            Done(1),
            MechanismsEntry(2),
            Done(2),
            Done(3),
            Done(4));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Local Error"), result);
        CollectionAssert.AreEqual(Hex.Bytes(Join(CapabilitiesSearch(1), MechanismsSearch(2), CapabilitiesSearch(3), MechanismsSearch(4), Unbind(5))), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapPackageCannotAnswerTheSicilyChallenge_FailsWithLocalError()
    {
        (TransferResult result, byte[] sent) = await RunLogonAsync(
            new FakeLogonTokenSource(2, NtlmNegotiate, null),
            Done(1),
            Done(2),
            Done(3),
            SicilyChallenge(4),
            Done(5),
            Done(6),
            Done(7),
            SicilyChallenge(8));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Local Error"), result);
        CollectionAssert.AreEqual(Hex.Bytes(Unbind(9)), sent[^Hex.Bytes(Unbind(9)).Length..]);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapPackageCannotAnswerTheSpnegoChallenge_FailsWithLocalError()
    {
        (TransferResult result, byte[] sent) = await RunLogonAsync(
            new FakeLogonTokenSource(2, NtlmNegotiate, null),
            CapabilitiesEntry(1),
            Done(1),
            MechanismsEntry(2),
            Done(2),
            SpnegoChallenge(3),
            Done(4),
            Done(5),
            SpnegoChallenge(6));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Local Error"), result);
        CollectionAssert.AreEqual(Hex.Bytes(Join(CapabilitiesSearch(1), MechanismsSearch(2), SpnegoBind(3), CapabilitiesSearch(4), MechanismsSearch(5), SpnegoBind(6), Unbind(7))), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapSpnegoSuccessBeforeTheAuthenticationIsComplete_FailsTheAttemptWithLocalError()
    {
        (TransferResult result, byte[] sent) = await RunLogonAsync(
            new FakeLogonTokenSource(2, NtlmNegotiate, NtlmAuthenticate),
            CapabilitiesEntry(1),
            Done(1),
            MechanismsEntry(2),
            Done(2),
            BindReply(3, 0),
            Done(4),
            Done(5),
            BindReply(6, 0));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Local Error"), result);
        CollectionAssert.AreEqual(Hex.Bytes(Join(CapabilitiesSearch(1), MechanismsSearch(2), SpnegoBind(3), CapabilitiesSearch(4), MechanismsSearch(5), SpnegoBind(6), Unbind(7))), sent);
    }

    [TestMethod]
    [DataRow(3, true)]
    [DataRow(4, false)]
    public async Task ExecuteAsync_WinLdapSpnegoSuccessCarryingTheServersLastToken_GivesItToThePackageFirst(int tokensToAuthenticate, bool completes)
    {
        var tokens = new FakeLogonTokenSource(tokensToAuthenticate, NtlmNegotiate, NtlmAuthenticate);

        (TransferResult result, _) = await RunLogonAsync(
            tokens,
            Done(1),
            MechanismsEntry(2),
            Done(2),
            SpnegoChallenge(3),
            "30 10 02 01 04 61 0b 0a 01 00 04 00 04 00 87 02 ab cd",
            completes ? Sealed(0, Done(5)) : Done(5),
            Done(6),
            SpnegoChallenge(7),
            "30 10 02 01 08 61 0b 0a 01 00 04 00 04 00 87 02 ab cd");

        CollectionAssert.AreEqual(Hex.Bytes("ab cd"), tokens.Challenges[2]);
        Assert.AreEqual(
            completes ? TransferResult.Success(0) : TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Local Error"),
            result);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapGssSpnegoUnderAnotherAttribute_IsNotAnOffer()
    {
        (TransferResult result, byte[] sent) = await RunLogonAsync(
            new FakeLogonTokenSource(2, NtlmNegotiate, NtlmAuthenticate),
            Done(1),
            "30 1c 02 01 02 64 17 04 00 30 13 30 11 04 01 78 31 0c 04 0a 47 53 53 2d 53 50 4e 45 47 4f",
            Done(2));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Timeout"), result);
        CollectionAssert.AreEqual(Hex.Bytes(Join(CapabilitiesSearch(1), MechanismsSearch(2), CapabilitiesSearch(3))), sent);
    }

    [TestMethod]
    [DataRow(false, null)]
    [DataRow(false, "30 05 02 01 04 04 00")]
    [DataRow(true, null)]
    [DataRow(true, "30 05 02 01 05 04 00")]
    public async Task ExecuteAsync_WinLdapSicilyBindUnanswered_FailsWith38TimeoutWithoutUnbinding(bool challenged, string? reply)
    {
        string[] replies = [Done(1), Done(2), Done(3), .. challenged ? new[] { SicilyChallenge(4) } : [], .. reply is null ? Array.Empty<string>() : new[] { reply }];

        (TransferResult result, byte[] sent) = await RunLogonAsync(new FakeLogonTokenSource(2, NtlmNegotiate, NtlmAuthenticate), replies);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Timeout"), result);
        string requests = Join(CapabilitiesSearch(1), MechanismsSearch(2), CapabilitiesSearch(3), SicilyNegotiateBind(4));
        CollectionAssert.AreEqual(Hex.Bytes(challenged ? Join(requests, SicilyResponseBind(5)) : requests), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapSpnegoBound_SealsTheSearchInABufferOf77BytesAsMeasured()
    {
        (_, byte[] sent) = await RunLogonAsync(
            new FakeLogonTokenSource(2, NtlmNegotiate, NtlmAuthenticate),
            CapabilitiesEntry(1),
            Done(1),
            MechanismsEntry(2),
            Done(2),
            SpnegoChallenge(3),
            BindReply(4, 0),
            Sealed(0, Done(5)));

        byte[] search = Hex.Bytes(Join(CapabilitiesSearch(1), MechanismsSearch(2), SpnegoBind(3), SpnegoAuthenticateBind(4)));
        CollectionAssert.AreEqual(Hex.Bytes($"00 00 00 4d {FakeLogonTokenSource.Signature(0)}"), sent[search.Length..(search.Length + 20)]);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapSealedBufferHoldingSeveralReplies_ReadsEachOfThem()
    {
        (TransferResult result, byte[] sent) = await RunLogonAsync(
            new FakeLogonTokenSource(2, NtlmNegotiate, NtlmAuthenticate),
            Done(1),
            Done(2),
            Done(3),
            SicilyChallenge(4),
            BindReply(5, 0),
            Sealed(0, Join("30 10 02 01 06 64 0b 04 03 6e 3d 78 30 04 30 02 04 00", Done(6))));

        // The entry's "DN: n=x" and two line ends: eight bytes written.
        Assert.AreEqual(TransferResult.Success(8), result);
        CollectionAssert.AreEqual(Hex.Bytes(Sealed(1, Unbind(7))), sent[^Hex.Bytes(Sealed(1, Unbind(7))).Length..]);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapEmptySealedBuffer_IsReadPast()
    {
        (TransferResult result, _) = await RunLogonAsync(
            new FakeLogonTokenSource(2, NtlmNegotiate, NtlmAuthenticate),
            Done(1),
            MechanismsEntry(2),
            Done(2),
            SpnegoChallenge(3),
            BindReply(4, 0),
            Sealed(0, string.Empty).TrimEnd(),
            Sealed(1, Done(5)));

        Assert.AreEqual(TransferResult.Success(0), result);
    }

    [TestMethod]
    [DataRow("00 00 00 14 02 00 00 00 5e 5e 5e 5e 5e 5e 5e 5e 00 00 00 00 30 0c 02 01 05 65 07 0a 01 00 04 00 04 00", DisplayName = "Signature that does not check")]
    [DataRow("30 0c 02 01 05 65 07 0a 01 00 04 00 04 00", DisplayName = "Unsealed reply")]
    [DataRow("01 00 00 01", DisplayName = "Length of one byte past 16 MiB")]
    [DataRow("00 00 00", DisplayName = "Closed in the length")]
    [DataRow("00 00 00 20 01 00 00 00", DisplayName = "Closed in the buffer")]
    public async Task ExecuteAsync_WinLdapReplyThatDoesNotUnwrap_FailsWith39ServerDownWithoutUnbinding(string reply)
    {
        (TransferResult result, byte[] sent) = await RunLogonAsync(
            new FakeLogonTokenSource(2, NtlmNegotiate, NtlmAuthenticate),
            CapabilitiesEntry(1),
            Done(1),
            MechanismsEntry(2),
            Done(2),
            SpnegoChallenge(3),
            BindReply(4, 0),
            reply);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapSearchFailed, "LDAP remote: Server Down"), result);
        CollectionAssert.AreEqual(Hex.Bytes(Join(CapabilitiesSearch(1), MechanismsSearch(2), SpnegoBind(3), SpnegoAuthenticateBind(4), Sealed(0, Search(5)))), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapSealedReplySplitAcrossReads_IsReadWhole()
    {
        string sealedDone = Sealed(0, Done(5));

        (TransferResult result, _) = await RunLogonAsync(
            new FakeLogonTokenSource(2, NtlmNegotiate, NtlmAuthenticate),
            Done(1),
            MechanismsEntry(2),
            Done(2),
            SpnegoChallenge(3),
            BindReply(4, 0),
            sealedDone[..5],
            sealedDone[6..20],
            sealedDone[21..]);

        Assert.AreEqual(TransferResult.Success(0), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapBound_KeepsTheBindsAuthenticationUntilTheTransferEnds()
    {
        var tokens = new FakeLogonTokenSource(2, NtlmNegotiate, NtlmAuthenticate);

        (TransferResult result, _) = await RunLogonAsync(
            tokens,
            CapabilitiesEntry(1),
            Done(1),
            MechanismsEntry(2),
            Done(2),
            BindReply(3, 0x31),
            Done(4),
            Done(5),
            SpnegoChallenge(6),
            BindReply(7, 0),
            Sealed(0, Done(8)));

        Assert.AreEqual(TransferResult.Success(0), result);
        Assert.AreEqual(2, tokens.Starts.Count);
        Assert.AreEqual(2, tokens.Disposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapWithoutUser_NeverStartsTheLogonAuthentication()
    {
        var tokens = new FakeLogonTokenSource(2, NtlmNegotiate, NtlmAuthenticate);
        var connection = new ScriptedConnection(Hex.Bytes(BindSuccess1), Hex.Bytes(SearchDone2));

        TransferResult result = await new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), LdapDialect.OpenLdap, tokens)
            .ExecuteAsync(Context("ldap://127.0.0.1:18389/dc=example", null));

        Assert.AreEqual(TransferResult.Success(0), result);
        Assert.IsEmpty(tokens.Starts);
    }

    private static async Task<(TransferResult Result, byte[] Sent)> RunLogonAsync(FakeLogonTokenSource tokens, params string[] replies)
    {
        var connection = new ScriptedConnection([.. replies.Select(Hex.Bytes)]);
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), LdapDialect.WinLdap, tokens);

        TransferResult result = await handler.ExecuteAsync(Context("ldap://127.0.0.1:18389/dc=example", null));

        return (result, connection.Sent);
    }

    private static string Join(params string[] messages) => string.Join(' ', messages);

    /// <summary>
    /// One SASL security layer buffer as WinLDAP frames it (BL-853): the sealed length in four
    /// big-endian octets, then the fake's signature for <paramref name="sequenceNumber" /> and
    /// <paramref name="message" />, which the fake seals as itself.
    /// </summary>
    private static string Sealed(int sequenceNumber, string message)
    {
        int length = 16 + Hex.Bytes(message).Length;
        return $"00 00 {length >> 8:x2} {length & 0xff:x2} {FakeLogonTokenSource.Signature(sequenceNumber)} {message}";
    }

    private static string CapabilitiesSearch(int id) =>
        $"30 84 00 00 00 44 02 01 {id:x2} 63 84 00 00 00 3b 04 00 0a 01 00 0a 01 00 02 01 00 02 01 78 01 01 00 87 0b 6f 62 6a 65 63 74 63 6c 61 73 73 30 84 00 00 00 17 04 15 73 75 70 70 6f 72 74 65 64 43 61 70 61 62 69 6c 69 74 69 65 73";

    private static string MechanismsSearch(int id) =>
        $"30 84 00 00 00 46 02 01 {id:x2} 63 84 00 00 00 3d 04 00 0a 01 00 0a 01 00 02 01 00 02 01 78 01 01 00 87 0b 6f 62 6a 65 63 74 63 6c 61 73 73 30 84 00 00 00 19 04 17 73 75 70 70 6f 72 74 65 64 53 41 53 4c 4d 65 63 68 61 6e 69 73 6d 73";

    private static string SpnegoBind(int id) =>
        $"30 84 00 00 00 62 02 01 {id:x2} 60 84 00 00 00 59 02 01 03 04 00 a3 84 00 00 00 4e 04 0a 47 53 53 2d 53 50 4e 45 47 4f 04 40 {NtlmNegotiate}";

    private static string SpnegoAuthenticateBind(int id) =>
        $"30 84 00 00 00 2e 02 01 {id:x2} 60 84 00 00 00 25 02 01 03 04 00 a3 84 00 00 00 1a 04 0a 47 53 53 2d 53 50 4e 45 47 4f 04 0c {NtlmAuthenticate}";

    private static string SicilyNegotiateBind(int id) =>
        $"30 84 00 00 00 54 02 01 {id:x2} 60 84 00 00 00 4b 02 01 03 04 04 4e 54 4c 4d 8a 40 {NtlmNegotiate}";

    private static string SicilyResponseBind(int id) =>
        $"30 84 00 00 00 1c 02 01 {id:x2} 60 84 00 00 00 13 02 01 03 04 00 8b 0c {NtlmAuthenticate}";

    private static string Search(int id) =>
        $"30 84 00 00 00 37 02 01 {id:x2} 63 84 00 00 00 2e 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 4f 62 6a 65 63 74 43 6c 61 73 73 30 84 00 00 00 00";

    private static string Unbind(int id) => $"30 84 00 00 00 05 02 01 {id:x2} 42 00";

    private static string Done(int id) => $"30 0c 02 01 {id:x2} 65 07 0a 01 00 04 00 04 00";

    private static string BindReply(int id, int resultCode) => $"30 0c 02 01 {id:x2} 61 07 0a 01 {resultCode:x2} 04 00 04 00";

    /// <summary>A <c>saslBindInProgress</c> BindResponse carrying <see cref="NtlmChallenge" /> as its serverSaslCreds.</summary>
    private static string SpnegoChallenge(int id) => $"30 1a 02 01 {id:x2} 61 15 0a 01 0e 04 00 04 00 87 0c {NtlmChallenge}";

    /// <summary>A <c>success</c> BindResponse carrying <see cref="NtlmChallenge" /> as its matchedDN, as a Sicily server answers <c>sicilyNegotiate</c>.</summary>
    private static string SicilyChallenge(int id) => $"30 18 02 01 {id:x2} 61 13 0a 01 00 04 0c {NtlmChallenge} 04 00";

    private static string CapabilitiesEntry(int id) =>
        $"30 3c 02 01 {id:x2} 64 37 04 00 30 33 30 31 04 15 73 75 70 70 6f 72 74 65 64 43 61 70 61 62 69 6c 69 74 69 65 73 31 18 04 16 31 2e 32 2e 38 34 30 2e 31 31 33 35 35 36 2e 31 2e 34 2e 38 30 30";

    /// <summary>A rootDSE entry whose <c>supportedSASLMechanisms</c> are <c>GSS-SPNEGO</c> and <c>GSSAPI</c>, as recorded.</summary>
    private static string MechanismsEntry(int id) =>
        $"30 3a 02 01 {id:x2} 64 35 04 00 30 31 30 2f 04 17 73 75 70 70 6f 72 74 65 64 53 41 53 4c 4d 65 63 68 61 6e 69 73 6d 73 31 14 04 0a 47 53 53 2d 53 50 4e 45 47 4f 04 06 47 53 53 41 50 49";
}
