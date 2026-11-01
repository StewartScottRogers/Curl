using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ldap.Fakes;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Pins the connection part of CGR-234 (BL-3999): without <c>-u</c>, curl 8.22.0's Schannel build
/// opens its own TCP connection, which carries no bytes, before WinLDAP opens the connection that
/// carries the binds and the search; with <c>-u</c>, and in the OpenLDAP build, there is one connection.
/// </summary>
[TestClass]
public sealed class Cgr234ConnectionTests
{
    private const string NtlmNegotiate = "4e 54 4c 4d 53 53 50 00 01 00 00 00";

    private static string Done(int id) => $"30 0c 02 01 {id:x2} 65 07 0a 01 00 04 00 04 00";

    private static string BindSuccess(int id) => $"30 0c 02 01 {id:x2} 61 07 0a 01 00 04 00 04 00";

    private static TransferContext Context() =>
        new() { Url = CurlUrl.Parse("ldap://127.0.0.1:21001/dc=bench"), Output = new MemoryStream() };

    [TestMethod]
    public async Task ExecuteAsync_WinLdapWithoutCredentials_OpensAnEmptyConnectionBeforeTheWinLdapConnection()
    {
        string[] replies = [Done(1), Done(2), Done(3), BindSuccess(4), Done(5), Done(6), Done(7), BindSuccess(8)];
        var curlConnection = new ScriptedConnection();
        var winLdapConnection = new ScriptedConnection([.. replies.Select(Hex.Bytes)]);
        var connector = new SequenceConnector(ConnectResult.Connected(curlConnection), ConnectResult.Connected(winLdapConnection));
        var handler = new LdapProtocolHandler(connector, LdapDialect.WinLdap, new FakeLogonTokenSource(2, NtlmNegotiate, NtlmNegotiate));

        TransferResult result = await handler.ExecuteAsync(Context());

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Protocol Error"), result);
        Assert.AreEqual(2, connector.Targets.Count);
        Assert.AreEqual(connector.Targets[0], connector.Targets[1]);
        Assert.IsEmpty(curlConnection.Sent);
        Assert.IsNotEmpty(winLdapConnection.Sent);
        Assert.IsTrue(curlConnection.IsDisposed);
        Assert.IsTrue(winLdapConnection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapConnectionFails_EndsWithItsFailureAndClosesTheEmptyConnection()
    {
        var curlConnection = new ScriptedConnection();
        var connector = new SequenceConnector(ConnectResult.Connected(curlConnection), ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect"));
        var handler = new LdapProtocolHandler(connector, LdapDialect.WinLdap, new FakeLogonTokenSource(2, NtlmNegotiate, NtlmNegotiate));

        TransferResult result = await handler.ExecuteAsync(Context());

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect", result.ErrorMessage);
        Assert.IsEmpty(curlConnection.Sent);
        Assert.IsTrue(curlConnection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapWithCredentials_OpensOneConnection()
    {
        var connector = new SequenceConnector(ConnectResult.Connected(new ScriptedConnection()));
        var handler = new LdapProtocolHandler(connector, LdapDialect.WinLdap, new FakeLogonTokenSource(2, NtlmNegotiate, NtlmNegotiate));
        var context = new TransferContext { Url = CurlUrl.Parse("ldap://127.0.0.1:21001/dc=bench"), Output = new MemoryStream(), Credentials = new NetworkCredential("user", "secret") };

        await handler.ExecuteAsync(context);

        Assert.HasCount(1, connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapWithoutCredentials_OpensOneConnection()
    {
        var connector = new SequenceConnector(ConnectResult.Connected(new ScriptedConnection()));
        var handler = new LdapProtocolHandler(connector, LdapDialect.OpenLdap, new FakeLogonTokenSource(2, NtlmNegotiate, NtlmNegotiate));

        await handler.ExecuteAsync(Context());

        Assert.HasCount(1, connector.Targets);
    }

    /// <summary>An <see cref="IConnector" /> that returns its results in order and records every target.</summary>
    private sealed class SequenceConnector(params ConnectResult[] results) : IConnector
    {
        public List<ConnectTarget> Targets { get; } = [];

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            Targets.Add(target);
            return ValueTask.FromResult(results[Targets.Count - 1]);
        }
    }
}
