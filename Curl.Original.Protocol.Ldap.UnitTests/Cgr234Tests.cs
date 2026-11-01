using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ldap.Fakes;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Pins CGR-234 (BL-3763): the Windows build's anonymous (negotiate) bind against a server that
/// answers <c>sicilyNegotiate</c> with a bare <c>success</c> (no NTLM challenge in the matchedDN)
/// fails with <c>curl: (38) LDAP local: bind via ldap_win_bind Protocol Error</c> (curl 8.22.0
/// Schannel, per the gap report), not <c>Local Error</c>.
/// </summary>
[TestClass]
public sealed class Cgr234Tests
{
    private const string NtlmNegotiate = "4e 54 4c 4d 53 53 50 00 01 00 00 00";

    private static string Done(int id) => $"30 0c 02 01 {id:x2} 65 07 0a 01 00 04 00 04 00";

    private static string BindSuccess(int id) => $"30 0c 02 01 {id:x2} 61 07 0a 01 00 04 00 04 00";

    [TestMethod]
    public async Task ExecuteAsync_WinLdapSicilyNegotiateAnsweredWithBareSuccess_FailsWithProtocolError()
    {
        string[] replies = [Done(1), Done(2), Done(3), BindSuccess(4), Done(5), Done(6), Done(7), BindSuccess(8)];
        var connection = new ScriptedConnection([.. replies.Select(Hex.Bytes)]);
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), LdapDialect.WinLdap, new FakeLogonTokenSource(2, NtlmNegotiate, NtlmNegotiate));
        var context = new TransferContext { Url = CurlUrl.Parse("ldap://127.0.0.1:21001/dc=bench"), Output = new MemoryStream() };

        TransferResult result = await handler.ExecuteAsync(context);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Protocol Error"), result);
    }
}
