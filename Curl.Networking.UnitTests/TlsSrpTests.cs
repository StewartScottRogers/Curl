using Curl.Networking.Fakes;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>Pins <see cref="TlsSrp" />: the login, the verbose lines and the suites an SRP hello offers (ADR-0328).</summary>
[TestClass]
public sealed class TlsSrpTests
{
    [TestMethod]
    public void CredentialsOf_WithUserAndPassword_IsTheLogin() =>
        Assert.AreEqual(new TlsSrpCredentials("alice", ""), TlsSrp.CredentialsOf(new TlsClientOptions(TlsUser: "alice", TlsPassword: "")));

    [TestMethod]
    [DataRow("alice", null)]
    [DataRow(null, "secret")]
    public void CredentialsOf_WithoutUserOrPassword_IsNull(string? user, string? password) =>
        Assert.IsNull(TlsSrp.CredentialsOf(new TlsClientOptions(TlsUser: user, TlsPassword: password)));

    [TestMethod]
    public void OfferedSuites_WithoutCiphers_KeepsTheTls13SuitesAndAppendsTheSrpList() =>
        CollectionAssert.AreEqual(
            new ushort[] { 0x1302, 0x1301, 0xc022, 0xc021, 0xc020, 0xc01f, 0xc01e, 0xc01d },
            TlsSrp.OfferedSuites(new TlsClientOptions(TlsUser: "alice"), [0x1302, 0xc02f, 0x1301, 0x009e]).ToArray());

    [TestMethod]
    public void OfferedSuites_WithCiphers_IsTheSuitesGiven()
    {
        ushort[] suites = [0x1301, 0xc02f];

        Assert.AreSame(suites, TlsSrp.OfferedSuites(new TlsClientOptions(TlsUser: "alice", Ciphers: "ECDHE-RSA-AES128-GCM-SHA256"), suites));
    }

    [TestMethod]
    public void ReportUser_ReportsTheUserName()
    {
        var events = new RecordingTransferEvents();

        TlsSrp.ReportUser(events, new TlsClientOptions(TlsUser: "alice"));

        CollectionAssert.AreEqual(new[] { "Using TLS-SRP username: alice" }, events.Info);
    }

    [TestMethod]
    [DataRow(null, new[] { "Setting cipher list SRP" })]
    [DataRow("ECDHE-RSA-AES128-GCM-SHA256", new string[0])]
    public void ReportCipherList_ReportsTheSrpListOnlyWithoutCiphers(string? ciphers, string[] expected)
    {
        var events = new RecordingTransferEvents();

        TlsSrp.ReportCipherList(events, new TlsClientOptions(TlsUser: "alice", Ciphers: ciphers));

        CollectionAssert.AreEqual(expected, events.Info);
    }

    [TestMethod]
    public void NullArguments_Throw()
    {
        var options = new TlsClientOptions();
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsSrp.CredentialsOf(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsSrp.ReportUser(null!, options));
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsSrp.ReportCipherList(null!, options));
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsSrp.ReportCipherList(new RecordingTransferEvents(), null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsSrp.ReportUser(new RecordingTransferEvents(), null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsSrp.OfferedSuites(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsSrp.OfferedSuites(options, null!));
    }
}
