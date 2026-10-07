using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Authentication;
using Curl.Protocol.Ssh.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// Pins how a known-hosts file is read and searched to what curl 8.21.0's Windows build
/// (libssh2 1.11.1) did on 2026-09-29 (BL-566): each "measured" case below reproduces a
/// known-hosts file given to the reference curl against a loopback SSH server on a port
/// other than 22, and whether curl accepted the host key.
/// </summary>
[TestClass]
public sealed partial class KnownHostsFileTests
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

    public TestContext TestContext { get; set; } = null!;

    internal static byte[] HostKey => TestHostKey.Ecdsa("nistp256", TestHostKey.FixedNistP256).Blob;

    [TestMethod]
    public void HostKey_IsTheFixedNistP256KeyBlob()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("expected base64", HostKeyBase64);

        string actual = Convert.ToBase64String(HostKey);

        diagnostics.Bytes("host key blob", HostKey);
        diagnostics.Act("host key base64", actual);
        diagnostics.Diff("host key base64", HostKeyBase64, actual);
        Assert.AreEqual(HostKeyBase64, actual);
    }

    [TestMethod]
    [DataRow("[127.0.0.1]:2222 ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "measured: [host]:port entry")]
    [DataRow("127.0.0.1 ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "measured: plain host entry on another port")]
    [DataRow("example.com,[127.0.0.1]:2222 ecdsa-sha2-nistp256 " + HostKeyBase64 + " comment\n", DisplayName = "measured: comma list with comment")]
    [DataRow("[127.0.0.1]:2222 ecdsa-sha2-nistp256 " + OtherKeyBase64 + "\n[127.0.0.1]:2222 ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "measured: a mismatch then a match")]
    [DataRow("|1|" + Salt + "|" + HashOfBracketedHost + " ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "measured: hashed [host]:port")]
    [DataRow("|1|" + Salt + "|" + HashOfPlainHost + " ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "measured: hashed plain host")]
    [DataRow("@revoked [127.0.0.1]:2222 ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n[127.0.0.1]:2222 ecdsa-sha2-nistp256 " + HostKeyBase64 + "\n", DisplayName = "measured: @revoked beside a plain entry")]
    [DataRow("# comment\n\n  \t[127.0.0.1]:2222\tecdsa-sha2-nistp256\t" + HostKeyBase64 + "\r\n", DisplayName = "comments, blank lines, tabs and CR LF")]
    public void Check_Accepted_IsMatch(string text)
    {
        KnownHostsCheck actual = CheckAndWrite(text, Host, Port, HostKey);

        TestDiagnostics.For(TestContext).Assert("check", KnownHostsCheck.Match, actual);
        Assert.AreEqual(KnownHostsCheck.Match, actual);
    }

    [TestMethod]
    [DataRow("[127.0.0.1]:2222 ecdsa-sha2-nistp256 " + OtherKeyBase64 + "\n", DisplayName = "measured: another key")]
    [DataRow("|1|" + Salt + "|" + HashOfBracketedHost + " ecdsa-sha2-nistp256 " + OtherKeyBase64 + "\n", DisplayName = "measured: hashed, another key")]
    [DataRow("127.0.0.1 ecdsa-sha2-nistp256 " + OtherKeyBase64 + "\n", DisplayName = "plain host, another key")]
    [DataRow("[127.0.0.1]:2222 ecdsa-sha2-nistp256\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\n", DisplayName = "an entry whose key is blanks")]
    public void Check_AnotherKeyForTheHost_IsMismatch(string text)
    {
        KnownHostsCheck actual = CheckAndWrite(text, Host, Port, HostKey);

        TestDiagnostics.For(TestContext).Assert("check", KnownHostsCheck.Mismatch, actual);
        Assert.AreEqual(KnownHostsCheck.Mismatch, actual);
    }

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
    public void Check_NoEntryForTheHostAndKeyType_IsNotFound(string text)
    {
        KnownHostsCheck actual = CheckAndWrite(text, Host, Port, HostKey);

        TestDiagnostics.For(TestContext).Assert("check", KnownHostsCheck.NotFound, actual);
        Assert.AreEqual(KnownHostsCheck.NotFound, actual);
    }

    [TestMethod]
    public void Check_OnPort22_SearchesThePlainHostOnly()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        KnownHostsFile file = KnownHostsFile.Parse($"[127.0.0.1]:22 ecdsa-sha2-nistp256 {HostKeyBase64}\n");
        diagnostics.Arrange("bracketed file", "[127.0.0.1]:22 ecdsa-sha2-nistp256 <host key>");
        diagnostics.Arrange("plain file", "127.0.0.1 ecdsa-sha2-nistp256 <host key>");
        diagnostics.Arrange("host and port", "127.0.0.1:22");

        KnownHostsCheck bracketed = file.Check(Host, 22, HostKey);
        KnownHostsCheck plain = KnownHostsFile.Parse($"127.0.0.1 ecdsa-sha2-nistp256 {HostKeyBase64}").Check(Host, 22, HostKey);

        diagnostics.Act("bracketed check", bracketed);
        diagnostics.Act("plain check", plain);
        diagnostics.Assert("bracketed check", KnownHostsCheck.NotFound, bracketed);
        diagnostics.Assert("plain check", KnownHostsCheck.Match, plain);
        Assert.AreEqual(KnownHostsCheck.NotFound, bracketed);
        Assert.AreEqual(KnownHostsCheck.Match, plain);
    }

    [TestMethod]
    public void Check_HostNames_AreCaseSensitive()
    {
        KnownHostsCheck actual = CheckAndWrite($"EXAMPLE.COM ecdsa-sha2-nistp256 {HostKeyBase64}", "example.com", 22, HostKey);

        TestDiagnostics.For(TestContext).Assert("check", KnownHostsCheck.NotFound, actual);
        Assert.AreEqual(
            KnownHostsCheck.NotFound,
            actual);
    }

    [TestMethod]
    public void Check_AHostKeyOfATypeLibssh2CannotCheck_IsNotFound()
    {
        byte[] dssKey = SshTestEncoding.Join(SshTestEncoding.Name("ssh-dss"), SshTestEncoding.String(new byte[20]));
        string text = $"127.0.0.1 ssh-dss {Convert.ToBase64String(dssKey)}";

        KnownHostsCheck actual = CheckAndWrite(text, Host, 22, dssKey);

        TestDiagnostics.For(TestContext).Assert("check", KnownHostsCheck.NotFound, actual);
        Assert.AreEqual(KnownHostsCheck.NotFound, actual);
    }

    [TestMethod]
    public void Parse_ReadsEachNameOfALineLastToFirst()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("known hosts", "a.example,b.example ssh-ed25519 <host key> comment");

        KnownHostsFile file = KnownHostsFile.Parse($"a.example,b.example ssh-ed25519 {HostKeyBase64} comment\n");

        string?[] names = file.Entries.Select(entry => entry.PlainName).ToArray();
        diagnostics.Act("entries", Entries(file));
        diagnostics.Assert("names", "[b.example, a.example]", $"[{string.Join(", ", names)}]");
        diagnostics.Assert("all ssh-ed25519 with the host key", true, file.Entries.All(entry => entry.KeyType == KnownHostKeyType.SshEd25519 && entry.Key == HostKeyBase64));
        CollectionAssert.AreEqual(new[] { "b.example", "a.example" }, names);
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
    public void Parse_ClassifiesTheKeyType(string typeName, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("key type name", typeName);

        string actual = KnownHostsFile.Parse($"host {typeName} {HostKeyBase64}").Entries.Single().KeyType.ToString();

        diagnostics.Act("key type", actual);
        diagnostics.Assert("key type", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Parse_AKeyWithoutTrailingText_KeepsTheKey()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("known hosts", "host ssh-rsa <host key> (no newline)");

        KnownHostsEntry entry = KnownHostsFile.Parse($"host ssh-rsa {HostKeyBase64}").Entries.Single();

        diagnostics.Act("key", entry.Key);
        diagnostics.Diff("key", HostKeyBase64, entry.Key);
        Assert.AreEqual(HostKeyBase64, entry.Key);
    }

    [TestMethod]
    public void Parse_ATypeWithNothingAfterIt_HasAnEmptyKey()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("known hosts", "host ssh-ed25519-and-more-text-here");

        string key = KnownHostsFile.Parse("host ssh-ed25519-and-more-text-here").Entries.Single().Key;

        diagnostics.Act("key", key);
        diagnostics.Diff("key", string.Empty, key);
        Assert.AreEqual(string.Empty, key);
    }

    [TestMethod]
    [DataRow("127.0.0.1", 2222, "127.0.0.1", DisplayName = "measured: plain host")]
    [DataRow("127.0.0.1", 2222, "[127.0.0.1]:2222", DisplayName = "measured: [host]:port")]
    [DataRow("127.0.0.10", 2222, "[127.0.0.1]:2222", DisplayName = "curl compares only the bracketed name's length")]
    [DataRow("127.0.0.1", 22, "[127.0.0.1]:22", DisplayName = "port 22 in brackets")]
    public void FindNarrowingEntry_NamesTheHost_FindsIt(string host, int port, string name)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("entry name", name);
        diagnostics.Arrange("host and port", $"{host}:{port}");

        string? found = KnownHostsFile.Parse($"{name} ssh-rsa {HostKeyBase64}").FindNarrowingEntry(host, port)?.PlainName;

        diagnostics.Act("narrowing entry", found ?? "(none)");
        diagnostics.Assert("narrowing entry", name, found ?? "(none)");
        Assert.AreEqual(name, found);
    }

    [TestMethod]
    [DataRow("[127.0.0.2]:2222", DisplayName = "measured: another host")]
    [DataRow("[127.0.0.1]:2223", DisplayName = "another port")]
    [DataRow("[127.0.0.1]:x", DisplayName = "a port that is not a number")]
    [DataRow("[127.0.0.1]:99999999999", DisplayName = "a port too large for a number")]
    [DataRow("[127.0.0.1]", DisplayName = "brackets without a port")]
    [DataRow("127.0.0.2", DisplayName = "another plain host")]
    public void FindNarrowingEntry_NamesAnotherHost_FindsNothing(string name)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("entry name", name);
        diagnostics.Arrange("host and port", $"{Host}:{Port}");

        KnownHostsEntry? found = KnownHostsFile.Parse($"{name} ssh-rsa {HostKeyBase64}").FindNarrowingEntry(Host, Port);

        diagnostics.Act("narrowing entry", found?.PlainName ?? "(none)");
        diagnostics.Assert("narrowing entry", "(none)", found?.PlainName ?? "(none)");
        Assert.IsNull(found);
    }

    [TestMethod]
    public void FindNarrowingEntry_AHashedEntry_IsFoundForAnyHost()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        KnownHostsFile file = KnownHostsFile.Parse($"|1|{Salt}|{HashOfPlainHost} ssh-ed25519 {HostKeyBase64}");
        diagnostics.Arrange("known hosts", $"|1|{Salt}|{HashOfPlainHost} ssh-ed25519 <host key>");
        diagnostics.Arrange("host and port", "elsewhere.example:22");

        KnownHostsEntry? found = file.FindNarrowingEntry("elsewhere.example", 22);

        diagnostics.Act("found the hashed entry", ReferenceEquals(file.Entries.Single(), found));
        diagnostics.Assert("found the hashed entry", true, ReferenceEquals(file.Entries.Single(), found));
        Assert.AreSame(file.Entries.Single(), found);
    }

    [TestMethod]
    public async Task LoadAsync_AnOpenedFile_ReadsItsEntries()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FakeFileSystem fileSystem = new($"[127.0.0.1]:2222 ecdsa-sha2-nistp256 {HostKeyBase64}\n");
        diagnostics.Arrange("file content", "[127.0.0.1]:2222 ecdsa-sha2-nistp256 <host key>");

        KnownHostsFile file = await KnownHostsFile.LoadAsync(fileSystem, "known_hosts", CancellationToken.None);

        KnownHostsCheck check = file.Check(Host, Port, HostKey);
        diagnostics.Act("opened path", fileSystem.OpenedPath);
        diagnostics.Act("check", check);
        diagnostics.Assert("opened path", "known_hosts", fileSystem.OpenedPath);
        diagnostics.Assert("check", KnownHostsCheck.Match, check);
        Assert.AreEqual("known_hosts", fileSystem.OpenedPath);
        Assert.AreEqual(KnownHostsCheck.Match, check);
    }

    [TestMethod]
    public async Task LoadAsync_AFileThatCannotBeOpened_ReadsAsEmpty()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("file", "missing (cannot be opened)");

        KnownHostsFile file = await KnownHostsFile.LoadAsync(new FakeFileSystem(null), "missing", CancellationToken.None);

        KnownHostsCheck check = file.Check(Host, Port, HostKey);
        diagnostics.Act("entry count", file.Entries.Count);
        diagnostics.Act("check", check);
        diagnostics.Assert("entry count", 0, file.Entries.Count);
        diagnostics.Assert("check", KnownHostsCheck.NotFound, check);
        Assert.AreEqual(0, file.Entries.Count);
        Assert.AreEqual(KnownHostsCheck.NotFound, check);
    }

    private static string Entries(KnownHostsFile file) =>
        $"{file.Entries.Count} entries [{string.Join(", ", file.Entries.Select(entry => $"{entry.PlainName ?? "(hashed)"} {entry.KeyType}"))}]";

    private KnownHostsCheck CheckAndWrite(string text, string host, int port, byte[] hostKey)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("known hosts", SshAuthenticationDiagnostics.Text(text));
        diagnostics.Arrange("host and port", $"{host}:{port}");
        diagnostics.Bytes("host key", hostKey);

        KnownHostsFile file = KnownHostsFile.Parse(text);
        KnownHostsCheck check = file.Check(host, port, hostKey);

        diagnostics.Act("entries", Entries(file));
        diagnostics.Act("check", check);
        return check;
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
