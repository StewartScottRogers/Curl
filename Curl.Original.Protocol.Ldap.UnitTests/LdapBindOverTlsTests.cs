using System.Net;
using Curl.Original.Protocol.Ldap.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Pins STD-143 (BL-5462): over <c>ldaps://</c> the Windows build binds LDAPv3 once, with no
/// LDAPv2 retry, so a wrong password fails with <c>curl: (38) LDAP local: bind via ldap_win_bind
/// Invalid Credentials</c> (curl 8.22.0 Schannel, per the fitness review), after an UnbindRequest.
/// </summary>
[TestClass]
public sealed class LdapBindOverTlsTests
{
    private const string BindV3 = "30 84 00 00 00 1f 02 01 01 60 84 00 00 00 16 02 01 03 04 09 63 6e 3d 75 2c 64 63 3d 78 80 06 73 65 63 72 65 74";

    private const string Unbind2 = "30 84 00 00 00 05 02 01 02 42 00";

    [TestMethod]
    [DataRow("31", "Invalid Credentials")]
    [DataRow("35", "Unwilling To Perform")]
    public async Task ExecuteAsync_WinLdapOverTlsBindRefused_UnbindsAndFailsWithTheFirstResultWithoutAnLdapV2Retry(string resultCode, string text)
    {
        var connection = new ScriptedConnection([Hex.Bytes($"30 0c 02 01 01 61 07 0a 01 {resultCode} 04 00 04 00")]);
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), LdapDialect.WinLdap);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ldaps://127.0.0.1:42636/ou=people,dc=example,dc=org?cn?sub?(uid=alice)"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("cn=u,dc=x", "secret"),
        };

        TransferResult result = await handler.ExecuteAsync(context);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind " + text), result);
        CollectionAssert.AreEqual(Hex.Bytes(BindV3 + " " + Unbind2), connection.Sent);
    }
}
