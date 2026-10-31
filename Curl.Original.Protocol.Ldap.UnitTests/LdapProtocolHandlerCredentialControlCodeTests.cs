using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Pins that an <c>ldap://</c> user name or password holding a byte below 0x20 is refused as
/// curl 8.22.0 refuses it, measured on 2026-10-13 (BL-2179): <c>curl: (43) control code detected
/// in credentials</c>, before any connection is made, so no bind is sent. Both builds refuse it.
/// </summary>
[TestClass]
public sealed class LdapProtocolHandlerCredentialControlCodeTests
{
    [TestMethod]
    [DataRow("a\tb", "p", LdapDialect.WinLdap, DisplayName = "user name with a tab, WinLDAP")]
    [DataRow("u", "p\r\nq", LdapDialect.WinLdap, DisplayName = "password with CR LF, WinLDAP")]
    [DataRow("u", "p\u001Fq", LdapDialect.OpenLdap, DisplayName = "password with 0x1F, OpenLDAP")]
    public async Task ExecuteAsync_CredentialsWithControlByte_FailsWithExit43WithoutConnecting(string user, string password, LdapDialect dialect)
    {
        var connector = new CountingConnector();

        TransferResult result = await new LdapProtocolHandler(connector, dialect).ExecuteAsync(Context(user, password));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.BadFunctionArgument, "control code detected in credentials"), result);
        Assert.AreEqual(0, connector.Calls);
    }

    [TestMethod]
    public async Task ExecuteAsync_CredentialsWithoutControlByte_Connects()
    {
        var connector = new CountingConnector();

        await new LdapProtocolHandler(connector, LdapDialect.WinLdap).ExecuteAsync(Context("u ~", "p !"));

        Assert.AreEqual(1, connector.Calls);
    }

    private static TransferContext Context(string user, string password) => new()
    {
        Url = CurlUrl.Parse("ldap://127.0.0.1/dc=example"),
        Output = new MemoryStream(),
        Credentials = new NetworkCredential(user, password),
    };

    private sealed class CountingConnector : IConnector
    {
        public int Calls { get; private set; }

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Could not connect to server"));
        }
    }
}
