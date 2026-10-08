using Curl.Protocol.Ssh.Authentication;
using Curl.Protocol.Ssh.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// Pins what <see cref="KnownHostsFile" /> gives curl 8.21.0's <c>-v</c> lines: whether
/// libssh2 would report the file unread, and the entry key a check names (BL-578, ADR-0262).
/// </summary>
public sealed partial class KnownHostsFileTests
{
    [TestMethod]
    public void Lookup_MismatchWithTwoEntries_NamesTheFirstEntrysKey()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        KnownHostsFile knownHosts = KnownHostsFile.Parse(
            $"[127.0.0.1]:2222 ecdsa-sha2-nistp256 {OtherKeyBase64}\n127.0.0.1 ecdsa-sha2-nistp256 AAAAAAAAAAAAAAAAAAAAAAAA\n");
        diagnostics.Arrange("known hosts", Entries(knownHosts));
        diagnostics.Arrange("host and port", $"{Host}:{Port}");

        KnownHostsLookup lookup = knownHosts.Lookup(Host, Port, HostKey);

        KnownHostsLookup expected = new(KnownHostsCheck.Mismatch, OtherKeyBase64);
        diagnostics.Act("lookup", lookup);
        diagnostics.Assert("lookup", expected, lookup);
        Assert.AreEqual(expected, lookup);
    }

    [TestMethod]
    [DataRow("", false, DisplayName = "an empty file is read")]
    [DataRow("# comment\n\n", false, DisplayName = "comments and blank lines are read")]
    [DataRow("host\n", true, DisplayName = "a line without a key is not")]
    public void Parse_ReadFailed_IsWhetherALineCouldNotBeParsed(string text, bool readFailed)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("known hosts", SshAuthenticationDiagnostics.Text(text));

        bool actual = KnownHostsFile.Parse(text).ReadFailed;

        diagnostics.Act("read failed", actual);
        diagnostics.Assert("read failed", readFailed, actual);
        Assert.AreEqual(readFailed, actual);
    }

    [TestMethod]
    public async Task LoadAsync_FileMissing_IsReadFailed()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("file system", "empty; kh is missing");

        KnownHostsFile knownHosts = await KnownHostsFile.LoadAsync(new InMemoryKeyFileSystem(new Dictionary<string, string>()), "kh", CancellationToken.None);

        diagnostics.Act("read failed", knownHosts.ReadFailed);
        diagnostics.Assert("read failed", true, knownHosts.ReadFailed);
        Assert.IsTrue(knownHosts.ReadFailed);
    }
}
