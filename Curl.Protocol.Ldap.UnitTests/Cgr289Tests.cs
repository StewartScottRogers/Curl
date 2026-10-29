using Curl.Protocol.Abstractions;
using Curl.Protocol.Ldap.Fakes;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Pins CGR-289 (BL-3818): curl 8.22.0's Schannel build opens WinLDAP's own <c>ldaps</c>
/// connection with the machine name as SNI and the system store as the only trust; the plaintext
/// connection and the connection curl itself opens are unchanged.
/// </summary>
[TestClass]
public sealed class Cgr289Tests
{
    private static async Task<List<ConnectTarget>> TargetsOfAsync(string url)
    {
        var connector = new RecordingTargets();
        var handler = new LdapProtocolHandler(connector, LdapDialect.WinLdap, new FakeLogonTokenSource(2, "4e", "4e"));

        await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream() });

        return connector.Targets;
    }

    [TestMethod]
    public async Task ExecuteAsync_LdapsWithoutCredentials_SecondConnectionSendsMachineNameAndTrustsSystemStore()
    {
        List<ConnectTarget> targets = await TargetsOfAsync("ldaps://127.0.0.1:21900/dc=test");

        Assert.HasCount(2, targets);
        Assert.IsNull(targets[0].TlsServerName);
        Assert.IsFalse(targets[0].TrustsSystemStoreOnly);
        Assert.AreEqual(Environment.MachineName, targets[1].TlsServerName);
        Assert.IsTrue(targets[1].TrustsSystemStoreOnly);
        Assert.AreEqual(targets[0].Host, targets[1].Host);
    }

    [TestMethod]
    public async Task ExecuteAsync_LdapWithoutCredentials_SecondConnectionKeepsTheTargetAsIs()
    {
        List<ConnectTarget> targets = await TargetsOfAsync("ldap://127.0.0.1:21900/dc=test");

        Assert.AreEqual(targets[0], targets[1]);
        Assert.IsNull(targets[1].TlsServerName);
    }

    private sealed class RecordingTargets : IConnector
    {
        public List<ConnectTarget> Targets { get; } = [];

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            Targets.Add(target);
            return ValueTask.FromResult(ConnectResult.Connected(new ScriptedConnection()));
        }
    }
}
