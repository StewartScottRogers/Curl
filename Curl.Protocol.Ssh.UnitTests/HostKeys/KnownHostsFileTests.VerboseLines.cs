using Curl.Protocol.Ssh.Fakes;

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
        KnownHostsFile knownHosts = KnownHostsFile.Parse(
            $"[127.0.0.1]:2222 ecdsa-sha2-nistp256 {OtherKeyBase64}\n127.0.0.1 ecdsa-sha2-nistp256 AAAAAAAAAAAAAAAAAAAAAAAA\n");

        KnownHostsLookup lookup = knownHosts.Lookup(Host, Port, HostKey);

        Assert.AreEqual(new KnownHostsLookup(KnownHostsCheck.Mismatch, OtherKeyBase64), lookup);
    }

    [TestMethod]
    [DataRow("", false, DisplayName = "an empty file is read")]
    [DataRow("# comment\n\n", false, DisplayName = "comments and blank lines are read")]
    [DataRow("host\n", true, DisplayName = "a line without a key is not")]
    public void Parse_ReadFailed_IsWhetherALineCouldNotBeParsed(string text, bool readFailed) =>
        Assert.AreEqual(readFailed, KnownHostsFile.Parse(text).ReadFailed);

    [TestMethod]
    public async Task LoadAsync_FileMissing_IsReadFailed()
    {
        KnownHostsFile knownHosts = await KnownHostsFile.LoadAsync(new InMemoryKeyFileSystem(new Dictionary<string, string>()), "kh", CancellationToken.None);

        Assert.IsTrue(knownHosts.ReadFailed);
    }
}
