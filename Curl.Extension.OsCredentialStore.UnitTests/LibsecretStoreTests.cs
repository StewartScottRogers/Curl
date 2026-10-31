namespace Curl.Extension.OsCredentialStore.UnitTests;

/// <summary>Against secret-tool(1)'s search --unlock: one block per item, its label, secret and attribute.* lines as key = value.</summary>
[TestClass]
public sealed class LibsecretStoreTests
{
    [TestMethod]
    public async Task FindAsync_ItemFound_GivesItsUserAttributeAndSecret()
    {
        RecordedCommandRunner runner = new(new CommandOutput(
            0,
            "[/org/freedesktop/secrets/collection/login/1]\nlabel = example\nsecret = s3cret\r\n",
            "created = 2026-10-29 10:00:00\nattribute.service = example.com\nattribute.user = alice\n"));

        var credential = await new LibsecretStore(runner).FindAsync("example.com", CancellationToken.None);

        Assert.AreEqual("secret-tool", runner.FileName);
        CollectionAssert.AreEqual(new[] { "search", "--unlock", "service", "example.com" }, runner.Arguments.ToArray());
        Assert.AreEqual("alice", credential!.UserName);
        Assert.AreEqual("s3cret", credential.Password);
    }

    [TestMethod]
    public async Task FindAsync_UsernameAttribute_IsTheUserWithoutAUserAttribute()
    {
        RecordedCommandRunner runner = new(new CommandOutput(0, "secret = pw\nattribute.username = bob\n", string.Empty));

        Assert.AreEqual("bob", (await new LibsecretStore(runner).FindAsync("x", CancellationToken.None))!.UserName);
    }

    [TestMethod]
    public async Task FindAsync_NoUserAttribute_GivesAnEmptyUser()
    {
        RecordedCommandRunner runner = new(new CommandOutput(0, "secret = pw\n", string.Empty));

        Assert.AreEqual(string.Empty, (await new LibsecretStore(runner).FindAsync("x", CancellationToken.None))!.UserName);
    }

    [TestMethod]
    public async Task FindAsync_NoItemOrFailedTool_GivesNone()
    {
        Assert.IsNull(await new LibsecretStore(new RecordedCommandRunner(new CommandOutput(0, string.Empty, string.Empty))).FindAsync("x", CancellationToken.None));
        Assert.IsNull(await new LibsecretStore(new RecordedCommandRunner(new CommandOutput(127, "secret = pw\n", "not found"))).FindAsync("x", CancellationToken.None));
        Assert.AreEqual("Secret Service (libsecret)", new LibsecretStore(new RecordedCommandRunner(new CommandOutput(0, string.Empty, string.Empty))).Name);
    }
}
