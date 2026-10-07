using Curl.Testing;
using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ldap.Fakes;

namespace Curl.Protocol.Ldap;

/// <content>
/// Pins how each reference build fails when its output stops accepting bytes or its connection
/// is reset, recorded with <c>Record-CurlExchange.ps1 -Script</c> on 2026-09-29 (BL-845) with
/// <c>-sS -u cn=u:p</c>: curl 8.21.0 with WinLDAP on Windows, standard output closed, for
/// <see cref="LdapDialect.WinLdap" />; curl 8.18.0 with OpenLDAP 2.6.10 on Linux, <c>-o /dev/full</c>,
/// for <see cref="LdapDialect.OpenLdap" />. <see cref="BufferOverflowRefusingStream" /> stands
/// in for the 4096-byte stdio buffer in front of the failed destination.
/// </content>
public sealed partial class LdapProtocolHandlerTests
{
    /// <summary>The OpenLDAP build's AbandonRequest for the search, messageID 3, then its UnbindRequest, messageID 4.</summary>
    private const string OpenLdapAbandon3Unbind4 = "30 06 02 01 03 50 01 02 30 05 02 01 04 42 00";

    /// <summary>A SearchResultEntry for <c>dc=a</c> with <c>cn: x</c>, messageID 2.</summary>
    private const string EntryDcA = "30 18 02 01 02 64 13 04 04 64 63 3d 61 30 0b 30 09 04 02 63 6e 31 03 04 01 78";

    [TestMethod]
    public async Task ExecuteAsync_WinLdapOutputFailsInTheHeldEntries_FailsWith23OnThePieceItFailedOnAndUnbinds()
    {
        (TransferResult result, byte[] sent) = await RunIntoFailingOutputAsync(LdapDialect.WinLdap, new BufferOverflowRefusingStream(4096));

        Diagnostics.Assert("result", new TransferResult(CurlExitCode.WriteError, 4092, "Failure writing output to destination, passed 37 returned 4"), result);
        Diagnostics.Diff("sent", Hex.Bytes(WinLdapBindCnU + " " + WinLdapSearchX + " " + WinLdapUnbind3), sent);
        Assert.AreEqual(new TransferResult(CurlExitCode.WriteError, 4092, "Failure writing output to destination, passed 37 returned 4"), result);
        CollectionAssert.AreEqual(Hex.Bytes(WinLdapBindCnU + " " + WinLdapSearchX + " " + WinLdapUnbind3), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapOutputFailsMidSearch_FailsWith23OnThePieceItFailedOnAbandonsAndUnbinds()
    {
        (TransferResult result, byte[] sent) = await RunIntoFailingOutputAsync(LdapDialect.OpenLdap, new BufferOverflowRefusingStream(4096));

        Diagnostics.Assert("result", new TransferResult(CurlExitCode.WriteError, 4060, "Failure writing output to destination, passed 37 returned 36"), result);
        Diagnostics.Diff("sent", Hex.Bytes(OpenLdapBindCnU + " " + OpenLdapSearchX + " " + OpenLdapAbandon3Unbind4), sent);
        Assert.AreEqual(new TransferResult(CurlExitCode.WriteError, 4060, "Failure writing output to destination, passed 37 returned 36"), result);
        CollectionAssert.AreEqual(Hex.Bytes(OpenLdapBindCnU + " " + OpenLdapSearchX + " " + OpenLdapAbandon3Unbind4), sent);
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap)]
    [DataRow(LdapDialect.OpenLdap)]
    public async Task ExecuteAsync_OutputThrowsAPlainIOException_ReportsNothingReturned(LdapDialect dialect)
    {
        (TransferResult result, _) = await RunIntoFailingOutputAsync(dialect, new IOExceptionThrowingStream());

        Diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.WriteError, "Failure writing output to destination, passed 4 returned 0"), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WriteError, "Failure writing output to destination, passed 4 returned 0"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapConnectionResetAfterAnEntry_FailsAsAServerThatClosed()
    {
        (TransferResult result, byte[] sent, byte[] output) = await RunOnResettingConnectionAsync(LdapDialect.WinLdap, int.MaxValue, BindSuccess1, EntryDcA);

        Diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.LdapSearchFailed, "LDAP remote: Server Down"), result);
        Diagnostics.Assert("output length", 0, output.Length);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapSearchFailed, "LDAP remote: Server Down"), result);
        Assert.AreEqual(0, output.Length);
        CollectionAssert.AreEqual(Hex.Bytes(WinLdapBindCnU + " " + WinLdapSearchX), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapConnectionResetAfterAnEntry_FailsWith56AfterWritingTheEntry()
    {
        (TransferResult result, byte[] sent, byte[] output) = await RunOnResettingConnectionAsync(LdapDialect.OpenLdap, int.MaxValue, BindSuccess1, EntryDcA);

        Diagnostics.Assert("result", new TransferResult(CurlExitCode.RecvError, 18, "LDAP local: search ldap_result Can't contact LDAP server"), result);
        Diagnostics.Diff("output", Encoding.Latin1.GetBytes("DN: dc=a\n\tcn: x\n\n\n"), output);
        Assert.AreEqual(new TransferResult(CurlExitCode.RecvError, 18, "LDAP local: search ldap_result Can't contact LDAP server"), result);
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes("DN: dc=a\n\tcn: x\n\n\n"), output);
        CollectionAssert.AreEqual(Hex.Bytes(OpenLdapBindCnU + " " + OpenLdapSearchX), sent);
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap, CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Timeout")]
    [DataRow(LdapDialect.OpenLdap, CurlExitCode.CouldntConnect, "LDAP local: connecting ldap_result Can't contact LDAP server")]
    public async Task ExecuteAsync_ConnectionResetInsteadOfTheBindResponse_FailsAsAServerThatClosed(LdapDialect dialect, CurlExitCode exitCode, string message)
    {
        (TransferResult result, _, _) = await RunOnResettingConnectionAsync(dialect, int.MaxValue);

        Diagnostics.Assert("result", TransferResult.Failure(exitCode, message), result);
        Assert.AreEqual(TransferResult.Failure(exitCode, message), result);
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap, CurlExitCode.LdapSearchFailed, "LDAP remote: Server Down")]
    [DataRow(LdapDialect.OpenLdap, CurlExitCode.RecvError, "LDAP local: search ldap_result Can't contact LDAP server")]
    public async Task ExecuteAsync_SearchRequestSentIntoAReset_FailsAsAServerThatClosed(LdapDialect dialect, CurlExitCode exitCode, string message)
    {
        (TransferResult result, _, _) = await RunOnResettingConnectionAsync(dialect, 1, BindSuccess1, SearchSuccess);

        Diagnostics.Assert("result", TransferResult.Failure(exitCode, message), result);
        Assert.AreEqual(TransferResult.Failure(exitCode, message), result);
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap, CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Timeout")]
    [DataRow(LdapDialect.OpenLdap, CurlExitCode.CouldntConnect, "LDAP local: connecting ldap_result Can't contact LDAP server")]
    public async Task ExecuteAsync_BindRequestSentIntoAReset_FailsAsAServerThatClosed(LdapDialect dialect, CurlExitCode exitCode, string message)
    {
        (TransferResult result, _, _) = await RunOnResettingConnectionAsync(dialect, 0, BindSuccess1);

        Diagnostics.Assert("result", TransferResult.Failure(exitCode, message), result);
        Assert.AreEqual(TransferResult.Failure(exitCode, message), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnbindRequestSentIntoAReset_StillSucceeds()
    {
        (TransferResult result, _, byte[] output) = await RunOnResettingConnectionAsync(LdapDialect.OpenLdap, 2, BindSuccess1, EntryDcA + " " + SearchSuccess);

        Diagnostics.Assert("result", TransferResult.Success(18), result);
        Diagnostics.Assert("output length", 18, output.Length);
        Assert.AreEqual(TransferResult.Success(18), result);
        Assert.AreEqual(18, output.Length);
    }

    /// <summary>
    /// The search BL-845 recorded the output failures with: 100 entries, each a 23-byte DN
    /// <c>dc=eNNNxxx…</c> and one 37-byte <c>description</c> value <c>vNNNyyy…</c>, then success.
    /// </summary>
    private static byte[] HundredEntries()
    {
        var replies = new List<byte>();
        for (int index = 0; index < 100; index++)
        {
            string dn = "dc=" + $"e{index:D3}".PadRight(20, 'x');
            string value = $"v{index:D3}".PadRight(37, 'y');
            byte[] attribute = Tlv(0x30, [.. OctetString("description"), .. Tlv(0x31, OctetString(value))]);
            byte[] entry = Tlv(0x64, [.. OctetString(dn), .. Tlv(0x30, attribute)]);
            replies.AddRange(Tlv(0x30, [0x02, 0x01, 0x02, .. entry]));
        }

        replies.AddRange(Hex.Bytes(SearchSuccess));
        return [.. replies];
    }

    private static byte[] OctetString(string text) => Tlv(0x04, Encoding.ASCII.GetBytes(text));

    /// <summary>A BER element with a short-form length, which every element here fits.</summary>
    private static byte[] Tlv(byte tag, byte[] content) => [tag, (byte)content.Length, .. content];

    /// <summary>Runs <paramref name="dialect" />'s handler, bound, on the recorded 100-entry search into <paramref name="output" />.</summary>
    private async Task<(TransferResult Result, byte[] Sent)> RunIntoFailingOutputAsync(LdapDialect dialect, Stream output)
    {
        var connection = new ScriptedConnection([Hex.Bytes(BindSuccess1), HundredEntries()]);
        Diagnostics.Bytes("replies", Hex.Bytes(BindSuccess1));
        TransferResult result = await RunSearchAsync(dialect, connection, output);
        Diagnostics.Bytes("sent", connection.Sent);
        return (result, connection.Sent);
    }

    /// <summary>
    /// Runs <paramref name="dialect" />'s handler on a connection that answers with
    /// <paramref name="replies" />, then resets, and that lets <paramref name="writesBeforeReset" /> writes through first.
    /// </summary>
    private async Task<(TransferResult Result, byte[] Sent, byte[] Output)> RunOnResettingConnectionAsync(LdapDialect dialect, int writesBeforeReset, params string[] replies)
    {
        var connection = new ResettingConnection(writesBeforeReset, [.. replies.Select(Hex.Bytes)]);
        var output = new MemoryStream();
        Diagnostics.Arrange("writes before reset", writesBeforeReset);
        Diagnostics.Arrange("replies", string.Join(" | ", replies));
        TransferResult result = await RunSearchAsync(dialect, connection, output);
        Diagnostics.Bytes("sent", connection.Sent);
        Diagnostics.Bytes("output", output.ToArray());
        return (result, connection.Sent, output.ToArray());
    }

    private async Task<TransferResult> RunSearchAsync(LdapDialect dialect, IConnection connection, Stream output)
    {
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), dialect);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ldap://127.0.0.1:38901/x"),
            Output = output,
            Credentials = new NetworkCredential("cn=u", "p"),
        };

        Diagnostics.Arrange("dialect", dialect);
        Diagnostics.Arrange("url", "ldap://127.0.0.1:38901/x");

        TransferResult result = await handler.ExecuteAsync(context);

        Diagnostics.Act("result", result);
        return result;
    }

    /// <summary>An output whose every write fails with a plain <see cref="IOException" />, which says nothing of what it took.</summary>
    private sealed class IOExceptionThrowingStream : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new IOException("The destination is gone.");
    }
}
