using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Pins <see cref="SshUserKeySource" />: curl 8.21.0's default key files, measured
/// 2026-09-29 (<c>HOME</c>'s <c>/.ssh/id_rsa</c> then <c>/.ssh/id_dsa</c>, joined with a
/// forward slash, then <c>id_rsa</c> and <c>id_dsa</c> in the working directory, then the
/// empty path, and no <c>USERPROFILE</c> fallback), read through the environment and file
/// system seams with platform-neutral paths.
/// </summary>
[TestClass]
public sealed class SshUserKeySourceTests
{
    private const string Home = "/home/tester";

    [TestMethod]
    [DataRow(new[] { Home + "/.ssh/id_rsa", Home + "/.ssh/id_dsa", "id_rsa" }, Home + "/.ssh/id_rsa", DisplayName = "HOME's id_rsa first, as measured")]
    [DataRow(new[] { Home + "/.ssh/id_dsa", "id_rsa" }, Home + "/.ssh/id_dsa", DisplayName = "then HOME's id_dsa, as measured")]
    [DataRow(new[] { "id_rsa", "id_dsa" }, "id_rsa", DisplayName = "then id_rsa in the working directory, as measured")]
    [DataRow(new[] { "id_dsa" }, "id_dsa", DisplayName = "then id_dsa")]
    [DataRow(new string[0], "", DisplayName = "else the empty path, as measured")]
    public async Task LocateAsync_NoKeyOption_TakesTheFirstDefaultThatExists(string[] existing, string expected)
    {
        SshUserKeySource source = Source(existing.ToDictionary(path => path, _ => "key"), Home, new SshOptions());

        SshUserKeyFiles files = await source.LocateAsync(CancellationToken.None);

        Assert.AreEqual(expected, files.PrivateKeyPath);
        Assert.IsNull(files.PublicKeyPath);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "HOME unset, as measured")]
    [DataRow("", DisplayName = "HOME empty")]
    public async Task LocateAsync_NoHome_LooksInTheWorkingDirectoryAlone(string? home)
    {
        InMemoryKeyFileSystem fileSystem = new(new Dictionary<string, string> { ["/.ssh/id_rsa"] = "key" });
        SshUserKeySource source = new(fileSystem, name => name == "HOME" ? home : "/elsewhere", new SshOptions(), Encoding.UTF8);

        SshUserKeyFiles files = await source.LocateAsync(CancellationToken.None);

        Assert.AreEqual(string.Empty, files.PrivateKeyPath);
        CollectionAssert.AreEqual(new[] { "id_rsa", "id_dsa" }, fileSystem.Opened.ToArray());
    }

    [TestMethod]
    public async Task LocateAsync_KeyAndPubkeyGiven_UsesThemWithoutLooking()
    {
        InMemoryKeyFileSystem fileSystem = new(new Dictionary<string, string>());
        SshUserKeySource source = new(fileSystem, _ => Home, new SshOptions { PrivateKeyPath = "k", PublicKeyPath = "k.pub" }, Encoding.UTF8);

        SshUserKeyFiles files = await source.LocateAsync(CancellationToken.None);

        Assert.AreEqual(new SshUserKeyFiles("k", "k.pub"), files);
        Assert.IsEmpty(fileSystem.Opened);
    }

    [TestMethod]
    public async Task LocateAsync_EmptyPubkey_DerivesThePublicKey()
    {
        SshUserKeySource source = Source([], Home, new SshOptions { PrivateKeyPath = "k", PublicKeyPath = string.Empty });

        Assert.IsNull((await source.LocateAsync(CancellationToken.None)).PublicKeyPath);
    }

    [TestMethod]
    public async Task ReadPrivateKeyAsync_EmptyPath_OpensNothing()
    {
        InMemoryKeyFileSystem fileSystem = new(new Dictionary<string, string>());
        SshUserKeySource source = new(fileSystem, _ => null, new SshOptions(), Encoding.UTF8);

        Assert.IsNull(await source.ReadPrivateKeyAsync(new SshUserKeyFiles(string.Empty, null), CancellationToken.None));
        Assert.IsEmpty(fileSystem.Opened);
    }

    [TestMethod]
    public async Task ReadPrivateKeyAsync_Passphrase_EncodedWithTheCredentialEncoding()
    {
        SshUserKeySource source = Source(new Dictionary<string, string> { ["k"] = TestUserKeys.RsaPkcs1Aes128 }, Home, new SshOptions { PrivateKeyPath = "k", PrivateKeyPassphrase = TestUserKeys.Passphrase });

        SshPrivateKey? key = await source.ReadPrivateKeyAsync(new SshUserKeyFiles("k", null), CancellationToken.None);

        Assert.IsNotNull(key);
    }

    [TestMethod]
    public async Task ReadPublicKeyAsync_WithoutPubkey_DerivesItFromThePrivateKey()
    {
        SshUserKeySource source = Source(new Dictionary<string, string> { ["k"] = TestUserKeys.EcdsaP256OpenSsh }, Home, new SshOptions());

        SshPublicKey? key = await source.ReadPublicKeyAsync(new SshUserKeyFiles("k", null), CancellationToken.None);

        CollectionAssert.AreEqual(SshPublicKeyFile.Parse(TestUserKeys.EcdsaP256PublicKeyFile)!.Blob, key!.Blob);
    }

    [TestMethod]
    public async Task ReadPublicKeyAsync_WithPubkey_ReadsItsFileAndNotThePrivateKey()
    {
        InMemoryKeyFileSystem fileSystem = new(new Dictionary<string, string> { ["k.pub"] = TestUserKeys.RsaPublicKeyFile });
        SshUserKeySource source = new(fileSystem, _ => null, new SshOptions(), Encoding.UTF8);

        SshPublicKey? key = await source.ReadPublicKeyAsync(new SshUserKeyFiles("k", "k.pub"), CancellationToken.None);

        Assert.AreEqual("ssh-rsa", key!.KeyType);
        CollectionAssert.AreEqual(new[] { "k.pub" }, fileSystem.Opened.ToArray());
    }

    [TestMethod]
    public async Task ReadPublicKeyAsync_PubkeyMissing_ReturnsNull()
    {
        SshUserKeySource source = Source([], Home, new SshOptions());

        Assert.IsNull(await source.ReadPublicKeyAsync(new SshUserKeyFiles("k", "k.pub"), CancellationToken.None));
    }

    private static SshUserKeySource Source(Dictionary<string, string> files, string? home, SshOptions options) =>
        new(new InMemoryKeyFileSystem(files), name => name == "HOME" ? home : null, options, Encoding.UTF8);
}
