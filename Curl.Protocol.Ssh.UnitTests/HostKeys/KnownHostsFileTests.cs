using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// Pins how a known-hosts file is read and searched to what curl 8.21.0's Windows build
/// (libssh2 1.11.1) did on 2026-09-29 (BL-566): each "measured" case below reproduces a
/// known-hosts file given to the reference curl against a loopback SSH server on a port
/// other than 22, and whether curl accepted the host key.
/// </summary>
[TestClass]
public sealed class KnownHostsFileTests
{
    internal const string HostKeyBase64 =
        "AAAAE2VjZHNhLXNoYTItbmlzdHAyNTYAAAAIbmlzdHAyNTYAAABBBJiizKUsfmEIwLjqUEuwvawTkyGivLftrsjESHWacn79Pc3zAok7zZJSKVKfI63nqYoM6v65EAxm9rAVGyJ6eMQ=";

    internal const string OtherKeyBase64 =
        "AAAAE2VjZHNhLXNoYTItbmlzdHAyNTYAAAAIbmlzdHAyNTYAAABBBAAAzKUsfmEIwLjqUEuwvawTkyGivLftrsjESHWacn79Pc3zAok7zZJSKVKfI63nqYoM6v65EAxm9rAVGyJ6eMQ=";

    // HMAC-SHA1 under the salt 01 02 .. 14 of "[127.0.0.1]:2222" and of "127.0.0.1".
    private const string Salt = "AQIDBAUGBwgJCgsMDQ4PEBESExQ=";

    private const string HashOfBracketedHost = "6nwC5rG6k7vxYwKIoAwPZGhI1VE=";

    private const string HashOfPlainHost = "Ht02luQ4iPpoalm1L8N0RLeng98=";

    private const string Host = "127.0.0.1";

    private const int Port = 2222;

    internal static byte[] HostKey => TestHostKey.Ecdsa("nistp256", TestHostKey.FixedNistP256).Blob;

    [TestMethod]
    public void HostKey_IsTheFixedNistP256KeyBlob() =>
        Assert.AreEqual(HostKeyBase64, Convert.ToBase64String(HostKey));

    [TestMethod]
    [DataRow("[127.0.0.1]:2222 ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "measured: [host]:port entry")]
    [DataRow("127.0.0.1 ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "measured: plain host entry on another port")]
    [DataRow("example.com,[127.0.0.1]:2222 ecdsa-sha2-nistp256 " + HostKeyBase64 + " comment\n", DisplayName = "measured: comma list with comment")]
    [DataRow("[127.0.0.1]:2222 ecdsa-sha2-nistp256 " + OtherKeyBase64 + "\n[127.0.0.1]:2222 ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "measured: a mismatch then a match")]
    [DataRow("|1|" + Salt + "|" + HashOfBracketedHost + " ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "measured: hashed [host]:port")]
    [DataRow("|1|" + Salt + "|" + HashOfPlainHost + " ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "measured: hashed plain host")]
    [DataRow("@revoked [127.0.0.1]:2222 ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n[127.0.0.1]:2222 ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "measured: @revoked beside a plain entry")]
    [DataRow("# comment\n\n  \t[127.0.0.1]:2222\tecdsa-sha2-nistp256\t" + HostKeyBase64 + "\r\n", DisplayName = "comments, blank lines, tabs and CR LF")]
    public void Check_Accepted_IsMatch(string text) =>
        Assert.AreEqual(KnownHostsCheck.Match, KnownHostsFile.Parse(text).Check(Host, Port, HostKey));

    [TestMethod]
    [DataRow("[127.0.0.1]:2222 ecdsa-sha2-nistp256 " + OtherKeyBase64 + "\n", DisplayName = "measured: another key")]
    [DataRow("|1|" + Salt + "|" + HashOfBracketedHost + " ecdsa-sha2-nistp256 " + OtherKeyBase64 + "\n", DisplayName = "measured: hashed, another key")]
    [DataRow("127.0.0.1 ecdsa-sha2-nistp256 " + OtherKeyBase64 + "\n", DisplayName = "plain host, another key")]
    [DataRow("[127.0.0.1]:2222 ecdsa-sha2-nistp256\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\n", DisplayName = "an entry whose key is blanks")]
    public void Check_AnotherKeyForTheHost_IsMismatch(string text) =>
        Assert.AreEqual(KnownHostsCheck.Mismatch, KnownHostsFile.Parse(text).Check(Host, Port, HostKey));

    [TestMethod]
    [DataRow("[127.0.0.2]:2222 ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "measured: another host")]
    [DataRow("[127.0.0.1]:2223 ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "measured: another port")]
    [DataRow("", DisplayName = "measured: empty file")]
    [DataRow("@revoked [127.0.0.1]:2222 ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "measured: @revoked alone")]
    [DataRow("@cert-authority [127.0.0.1]:2222 ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "measured: @cert-authority")]
    [DataRow("[127.0.0.1]:2222 ssh-rsa AAAA\n[127.0.0.1]:2222 ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "measured: an unparsable line ends the reading")]
    [DataRow("[127.0.0.1]:2222 ssh-ed25519 " + HostKeyBase64 + "\n", DisplayName = "an entry of another type")]
    [DataRow("[127.0.0.1]:2222 ecdsa-sha2-nistp256-cert-v01@openssh.com " + HostKeyBase64 + "\n", DisplayName = "an entry of an unknown type")]
    [DataRow("[127.0.0.1]:2222 2048 65537 " + HostKeyBase64 + "\n", DisplayName = "an RSA1 entry")]
    [DataRow("[127.0.0.1]:2222\n[127.0.0.1]:2222 ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "a line with names only ends the reading")]
    [DataRow("[127.0.0.1]:2222 ecdsa-sha2-nistp256\n", DisplayName = "a key part under 20 characters")]
    [DataRow("|1|" + Salt + " ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "a hashed name without its hash is skipped")]
    [DataRow("|1|" + Salt + "|A ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n[127.0.0.1]:2222 ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "broken hash base64 ends the reading")]
    [DataRow("|1|A|" + HashOfBracketedHost + " ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "broken salt base64 ends the reading")]
    [DataRow("|1|" + Salt + "|" + Salt + "AAAA ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "a hash not 20 bytes long never matches")]
    public void Check_NoEntryForTheHostAndKeyType_IsNotFound(string text) =>
        Assert.AreEqual(KnownHostsCheck.NotFound, KnownHostsFile.Parse(text).Check(Host, Port, HostKey));

    [TestMethod]
    public void Check_OnPort22_SearchesThePlainHostOnly()
    {
        KnownHostsFile file = KnownHostsFile.Parse($"[127.0.0.1]:22 ecdsa-sha2-nistp256 {HostKeyBase64}\n");

        Assert.AreEqual(KnownHostsCheck.NotFound, file.Check(Host, 22, HostKey));
        Assert.AreEqual(KnownHostsCheck.Match, KnownHostsFile.Parse($"127.0.0.1 ecdsa-sha2-nistp256 {HostKeyBase64}").Check(Host, 22, HostKey));
    }

    [TestMethod]
    public void Check_HostNames_AreCaseSensitive() =>
        Assert.AreEqual(
            KnownHostsCheck.NotFound,
            KnownHostsFile.Parse($"EXAMPLE.COM ecdsa-sha2-nistp256 {HostKeyBase64}").Check("example.com", 22, HostKey));

    [TestMethod]
    public void Check_AHostKeyOfATypeLibssh2CannotCheck_IsNotFound()
    {
        byte[] dssKey = SshTestEncoding.Join(SshTestEncoding.Name("ssh-dss"), SshTestEncoding.String(new byte[20]));
        string text = $"127.0.0.1 ssh-dss {Convert.ToBase64String(dssKey)}";

        Assert.AreEqual(KnownHostsCheck.NotFound, KnownHostsFile.Parse(text).Check(Host, 22, dssKey));
    }

    [TestMethod]
    public void Parse_ReadsEachNameOfALineLastToFirst()
    {
        KnownHostsFile file = KnownHostsFile.Parse($"a.example,b.example ssh-ed25519 {HostKeyBase64} comment\n");

        CollectionAssert.AreEqual(new[] { "b.example", "a.example" }, file.Entries.Select(entry => entry.PlainName).ToArray());
        Assert.IsTrue(file.Entries.All(entry => entry.KeyType == KnownHostKeyType.SshEd25519 && entry.Key == HostKeyBase64));
    }

    [TestMethod]
    [DataRow("ssh-rsa", nameof(KnownHostKeyType.SshRsa))]
    [DataRow("ecdsa-sha2-nistp256", nameof(KnownHostKeyType.EcdsaNistP256))]
    [DataRow("ecdsa-sha2-nistp384", nameof(KnownHostKeyType.EcdsaNistP384))]
    [DataRow("ecdsa-sha2-nistp521", nameof(KnownHostKeyType.EcdsaNistP521))]
    [DataRow("ssh-ed25519", nameof(KnownHostKeyType.SshEd25519))]
    [DataRow("ssh-dss", nameof(KnownHostKeyType.Unknown))]
    [DataRow("2048", nameof(KnownHostKeyType.Rsa1))]
    public void Parse_ClassifiesTheKeyType(string typeName, string expected) =>
        Assert.AreEqual(expected, KnownHostsFile.Parse($"host {typeName} {HostKeyBase64}").Entries.Single().KeyType.ToString());

    [TestMethod]
    public void Parse_AKeyWithoutTrailingText_KeepsTheKey()
    {
        KnownHostsEntry entry = KnownHostsFile.Parse($"host ssh-rsa {HostKeyBase64}").Entries.Single();

        Assert.AreEqual(HostKeyBase64, entry.Key);
    }

    [TestMethod]
    public void Parse_ATypeWithNothingAfterIt_HasAnEmptyKey() =>
        Assert.AreEqual(string.Empty, KnownHostsFile.Parse("host ssh-ed25519-and-more-text-here").Entries.Single().Key);

    [TestMethod]
    [DataRow("127.0.0.1", 2222, "127.0.0.1", DisplayName = "measured: plain host")]
    [DataRow("127.0.0.1", 2222, "[127.0.0.1]:2222", DisplayName = "measured: [host]:port")]
    [DataRow("127.0.0.10", 2222, "[127.0.0.1]:2222", DisplayName = "curl compares only the bracketed name's length")]
    [DataRow("127.0.0.1", 22, "[127.0.0.1]:22", DisplayName = "port 22 in brackets")]
    public void FindNarrowingEntry_NamesTheHost_FindsIt(string host, int port, string name) =>
        Assert.AreEqual(name, KnownHostsFile.Parse($"{name} ssh-rsa {HostKeyBase64}").FindNarrowingEntry(host, port)?.PlainName);

    [TestMethod]
    [DataRow("[127.0.0.2]:2222", DisplayName = "measured: another host")]
    [DataRow("[127.0.0.1]:2223", DisplayName = "another port")]
    [DataRow("[127.0.0.1]:x", DisplayName = "a port that is not a number")]
    [DataRow("[127.0.0.1]:99999999999", DisplayName = "a port too large for a number")]
    [DataRow("[127.0.0.1]", DisplayName = "brackets without a port")]
    [DataRow("127.0.0.2", DisplayName = "another plain host")]
    public void FindNarrowingEntry_NamesAnotherHost_FindsNothing(string name) =>
        Assert.IsNull(KnownHostsFile.Parse($"{name} ssh-rsa {HostKeyBase64}").FindNarrowingEntry(Host, Port));

    [TestMethod]
    public void FindNarrowingEntry_AHashedEntry_IsFoundForAnyHost()
    {
        KnownHostsFile file = KnownHostsFile.Parse($"|1|{Salt}|{HashOfPlainHost} ssh-ed25519 {HostKeyBase64}");

        Assert.AreSame(file.Entries.Single(), file.FindNarrowingEntry("elsewhere.example", 22));
    }

    [TestMethod]
    public async Task LoadAsync_AnOpenedFile_ReadsItsEntries()
    {
        FakeFileSystem fileSystem = new($"[127.0.0.1]:2222 ecdsa-sha2-nistp256 {HostKeyBase64}\n");

        KnownHostsFile file = await KnownHostsFile.LoadAsync(fileSystem, "known_hosts", CancellationToken.None);

        Assert.AreEqual("known_hosts", fileSystem.OpenedPath);
        Assert.AreEqual(KnownHostsCheck.Match, file.Check(Host, Port, HostKey));
    }

    [TestMethod]
    public async Task LoadAsync_AFileThatCannotBeOpened_ReadsAsEmpty()
    {
        KnownHostsFile file = await KnownHostsFile.LoadAsync(new FakeFileSystem(null), "missing", CancellationToken.None);

        Assert.AreEqual(0, file.Entries.Count);
        Assert.AreEqual(KnownHostsCheck.NotFound, file.Check(Host, Port, HostKey));
    }

    private sealed class FakeFileSystem(string? content) : IFileSystem
    {
        public string? OpenedPath { get; private set; }

        public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken)
        {
            OpenedPath = path;
            return ValueTask.FromResult(content is null
                ? FileOpenResult.Failed(FileAccessStatus.NotFound)
                : FileOpenResult.Opened(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)), content.Length, null));
        }

        public ValueTask<FileOpenResult> OpenForWriteAsync(string path, FileWriteMode mode, UnixFileMode createMode, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
