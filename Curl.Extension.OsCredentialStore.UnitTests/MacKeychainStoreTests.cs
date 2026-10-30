namespace Curl.Extension.OsCredentialStore.UnitTests;

/// <summary>Against security(1)'s find-generic-password -g: attributes on standard output, the password on standard error, exit 44 when no item matches.</summary>
[TestClass]
public sealed class MacKeychainStoreTests
{
    private const string Attributes =
        "keychain: \"/Users/alice/Library/Keychains/login.keychain-db\"\n" +
        "version: 512\n" +
        "class: \"genp\"\n" +
        "attributes:\n" +
        "    0x00000007 <blob>=\"example.com\"\n" +
        "    \"acct\"<blob>=\"alice\"\n" +
        "    \"svce\"<blob>=\"example.com\"\n";

    [TestMethod]
    public async Task FindAsync_ItemFound_GivesItsAccountAndPassword()
    {
        RecordedCommandRunner runner = new(new CommandOutput(0, Attributes, "password: \"s3cret\"\n"));

        var credential = await new MacKeychainStore(runner).FindAsync("example.com", CancellationToken.None);

        Assert.AreEqual("/usr/bin/security", runner.FileName);
        CollectionAssert.AreEqual(new[] { "find-generic-password", "-g", "-s", "example.com" }, runner.Arguments.ToArray());
        Assert.AreEqual("alice", credential!.UserName);
        Assert.AreEqual("s3cret", credential.Password);
    }

    [TestMethod]
    public async Task FindAsync_PasswordNotPrintable_DecodesItsHexadecimal()
    {
        RecordedCommandRunner runner = new(new CommandOutput(0, Attributes, "password: 0x70C3A4737321  \"p\\303\\244ss!\"\n"));

        var credential = await new MacKeychainStore(runner).FindAsync("example.com", CancellationToken.None);

        Assert.AreEqual("päss!", credential!.Password);
    }

    [TestMethod]
    public async Task FindAsync_NoAccountOrPasswordPrinted_GivesThemEmpty()
    {
        RecordedCommandRunner runner = new(new CommandOutput(0, "attributes:\n", string.Empty));

        var credential = await new MacKeychainStore(runner).FindAsync("example.com", CancellationToken.None);

        Assert.AreEqual(string.Empty, credential!.UserName);
        Assert.AreEqual(string.Empty, credential.Password);
    }

    [TestMethod]
    public async Task FindAsync_NoItemMatches_GivesNone()
    {
        var store = new MacKeychainStore(new RecordedCommandRunner(new CommandOutput(
            44, string.Empty, "security: SecKeychainSearchCopyNext: The specified item could not be found in the keychain.\n")));

        Assert.IsNull(await store.FindAsync("missing", CancellationToken.None));
        Assert.AreEqual("macOS keychain", store.Name);
    }
}
