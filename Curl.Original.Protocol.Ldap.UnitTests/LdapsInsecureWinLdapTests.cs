using System.Net;
using Curl.Original.Protocol.Ldap.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Pins BL-5285: over <c>ldaps</c> with <c>-u</c>, WinLDAP checks the certificate against the
/// Windows store itself, so a connection the system store does not trust ends the bind with
/// <c>Server Down</c> (exit 38) whatever <c>-k</c> says.
/// </summary>
[TestClass]
public sealed class LdapsInsecureWinLdapTests
{
    private static readonly NetworkCredential Alice = new("alice", "secret");

    private static async Task<(TransferResult Result, RecordingConnector Connector)> RunAsync(LdapDialect dialect, string url, ConnectResult connect)
    {
        var connector = new RecordingConnector(connect);
        var handler = new LdapProtocolHandler(connector, dialect, new FakeLogonTokenSource(0));

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream(), Credentials = Alice });

        return (result, connector);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapLdapsWithCredentialsUntrusted_FailsWithServerDown()
    {
        (TransferResult result, RecordingConnector connector) = await RunAsync(LdapDialect.WinLdap, "ldaps://127.0.0.1:21900/dc=test", ConnectResult.Failed(CurlExitCode.PeerFailedVerification, "untrusted"));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Server Down"), result);
        Assert.IsTrue(connector.Targets.Single().TrustsSystemStoreOnly);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapLdapsWithCredentialsUntrusted_KeepsTheConnectFailure()
    {
        (TransferResult result, RecordingConnector connector) = await RunAsync(LdapDialect.OpenLdap, "ldaps://127.0.0.1:21900/dc=test", ConnectResult.Failed(CurlExitCode.PeerFailedVerification, "untrusted"));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.IsFalse(connector.Targets.Single().TrustsSystemStoreOnly);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapLdapWithCredentialsRefused_KeepsTheConnectFailure()
    {
        (TransferResult result, RecordingConnector connector) = await RunAsync(LdapDialect.WinLdap, "ldap://127.0.0.1:21900/dc=test", ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused"));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsFalse(connector.Targets.Single().TrustsSystemStoreOnly);
    }
}
