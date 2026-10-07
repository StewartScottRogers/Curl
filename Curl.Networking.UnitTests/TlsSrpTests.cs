using Curl.Networking.Fakes;
using Curl.Testing;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>Pins <see cref="TlsSrp" />: the login, the verbose lines and the suites an SRP hello offers (ADR-0328).</summary>
[TestClass]
public sealed class TlsSrpTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void CredentialsOf_WithUserAndPassword_IsTheLogin()
    {
        Diagnostics.Arrange("tls user, tls password", "alice, empty");

        var credentials = TlsSrp.CredentialsOf(new TlsClientOptions(TlsUser: "alice", TlsPassword: ""));

        Diagnostics.Act("credentials", credentials?.ToString() ?? "null");
        Diagnostics.Assert("credentials", new TlsSrpCredentials("alice", "").ToString(), credentials?.ToString() ?? "null");

        Assert.AreEqual(new TlsSrpCredentials("alice", ""), TlsSrp.CredentialsOf(new TlsClientOptions(TlsUser: "alice", TlsPassword: "")));
    }

    [TestMethod]
    [DataRow("alice", null)]
    [DataRow(null, "secret")]
    public void CredentialsOf_WithoutUserOrPassword_IsNull(string? user, string? password)
    {
        Diagnostics.Arrange("tls user, tls password", $"{user ?? "null"}, {(password is null ? "null" : "given")}");

        var credentials = TlsSrp.CredentialsOf(new TlsClientOptions(TlsUser: user, TlsPassword: password));

        Diagnostics.Act("credentials", credentials is null ? "null" : "login");
        Diagnostics.Assert("credentials", "null", credentials is null ? "null" : "login");

        Assert.IsNull(TlsSrp.CredentialsOf(new TlsClientOptions(TlsUser: user, TlsPassword: password)));
    }

    [TestMethod]
    public void OfferedSuites_WithoutCiphers_KeepsTheTls13SuitesAndAppendsTheSrpList()
    {
        ushort[] given = [0x1302, 0xc02f, 0x1301, 0x009e];
        Diagnostics.Arrange("suites given", string.Join(", ", given.Select(suite => $"0x{suite:x4}")));

        var offered = TlsSrp.OfferedSuites(new TlsClientOptions(TlsUser: "alice"), given).ToArray();

        Diagnostics.Act("suites offered", string.Join(", ", offered.Select(suite => $"0x{suite:x4}")));
        Diagnostics.Assert("suite count", 8, offered.Length);

        CollectionAssert.AreEqual(
            new ushort[] { 0x1302, 0x1301, 0xc022, 0xc021, 0xc020, 0xc01f, 0xc01e, 0xc01d },
            TlsSrp.OfferedSuites(new TlsClientOptions(TlsUser: "alice"), [0x1302, 0xc02f, 0x1301, 0x009e]).ToArray());
    }

    [TestMethod]
    public void OfferedSuites_WithCiphers_IsTheSuitesGiven()
    {
        ushort[] suites = [0x1301, 0xc02f];
        Diagnostics.Arrange("suites given", "0x1301, 0xc02f with a ciphers list");

        var offered = TlsSrp.OfferedSuites(new TlsClientOptions(TlsUser: "alice", Ciphers: "ECDHE-RSA-AES128-GCM-SHA256"), suites);

        Diagnostics.Act("same array returned", ReferenceEquals(suites, offered));
        Diagnostics.Assert("same array returned", true, ReferenceEquals(suites, offered));

        Assert.AreSame(suites, TlsSrp.OfferedSuites(new TlsClientOptions(TlsUser: "alice", Ciphers: "ECDHE-RSA-AES128-GCM-SHA256"), suites));
    }

    [TestMethod]
    public void ReportUser_ReportsTheUserName()
    {
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("tls user", "alice");

        TlsSrp.ReportUser(events, new TlsClientOptions(TlsUser: "alice"));

        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Diagnostics.Assert("info lines", "Using TLS-SRP username: alice", string.Join(" | ", events.Info));

        CollectionAssert.AreEqual(new[] { "Using TLS-SRP username: alice" }, events.Info);
    }

    [TestMethod]
    [DataRow(null, new[] { "Setting cipher list SRP" })]
    [DataRow("ECDHE-RSA-AES128-GCM-SHA256", new string[0])]
    public void ReportCipherList_ReportsTheSrpListOnlyWithoutCiphers(string? ciphers, string[] expected)
    {
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("ciphers", ciphers ?? "null");

        TlsSrp.ReportCipherList(events, new TlsClientOptions(TlsUser: "alice", Ciphers: ciphers));

        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Diagnostics.Assert("info lines", string.Join(" | ", expected), string.Join(" | ", events.Info));

        CollectionAssert.AreEqual(expected, events.Info);
    }

    [TestMethod]
    public void NullArguments_Throw()
    {
        var options = new TlsClientOptions();
        Diagnostics.Arrange("null arguments", "CredentialsOf, ReportUser, ReportCipherList, OfferedSuites");

        var exceptions = new[]
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => TlsSrp.CredentialsOf(null!)),
            Assert.ThrowsExactly<ArgumentNullException>(() => TlsSrp.ReportUser(null!, options)),
            Assert.ThrowsExactly<ArgumentNullException>(() => TlsSrp.ReportCipherList(null!, options)),
            Assert.ThrowsExactly<ArgumentNullException>(() => TlsSrp.ReportCipherList(new RecordingTransferEvents(), null!)),
            Assert.ThrowsExactly<ArgumentNullException>(() => TlsSrp.ReportUser(new RecordingTransferEvents(), null!)),
            Assert.ThrowsExactly<ArgumentNullException>(() => TlsSrp.OfferedSuites(null!, [])),
            Assert.ThrowsExactly<ArgumentNullException>(() => TlsSrp.OfferedSuites(options, null!)),
        };

        Diagnostics.Act("parameter names", string.Join(", ", exceptions.Select(exception => exception.ParamName)));
        Diagnostics.Assert("exceptions thrown", 7, exceptions.Length);
    }
}
