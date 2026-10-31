namespace Curl.Extension.SshJump;

[TestClass]
public sealed class SshJumpHostTests
{
    [TestMethod]
    [DataRow("alice@bastion.example", "alice", null, "bastion.example", 22)]
    [DataRow("alice@bastion.example:2222", "alice", null, "bastion.example", 2222)]
    [DataRow("alice:s3cret@bastion.example:1", "alice", "s3cret", "bastion.example", 1)]
    [DataRow("alice:p:w@x@10.0.0.1:65535", "alice", "p:w@x", "10.0.0.1", 65535)]
    [DataRow("alice@[2001:db8::1]", "alice", null, "2001:db8::1", 22)]
    [DataRow("alice@[2001:db8::1]:2200", "alice", null, "2001:db8::1", 2200)]
    public void TryParse_UserHostAndPort_ReadsEachPart(string text, string user, string? password, string host, int port)
    {
        Assert.IsTrue(SshJumpHost.TryParse(text, out SshJumpHost? jumpHost));

        Assert.AreEqual(new SshJumpHost(user, password, host, port), jumpHost);
    }

    [TestMethod]
    [DataRow("bastion.example")]
    [DataRow("@bastion.example")]
    [DataRow(":pw@bastion.example")]
    [DataRow("alice@")]
    [DataRow("alice@:22")]
    [DataRow("alice@bastion.example:")]
    [DataRow("alice@bastion.example:0")]
    [DataRow("alice@bastion.example:65536")]
    [DataRow("alice@bastion.example:+22")]
    [DataRow("alice@bastion.example:22:23")]
    [DataRow("alice@[]")]
    [DataRow("alice@[::1")]
    [DataRow("alice@[::1]22")]
    public void TryParse_NotAJumpHost_ReturnsFalse(string text)
    {
        Assert.IsFalse(SshJumpHost.TryParse(text, out SshJumpHost? jumpHost));

        Assert.IsNull(jumpHost);
    }

    [TestMethod]
    public void TryParse_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => SshJumpHost.TryParse(null!, out _));

    [TestMethod]
    public void ToString_NeverShowsThePasswordAndBracketsAnIpv6Host()
    {
        Assert.AreEqual("alice@bastion.example:22", new SshJumpHost("alice", "s3cret", "bastion.example", 22).ToString());
        Assert.AreEqual("alice@[::1]:2222", new SshJumpHost("alice", null, "::1", 2222).ToString());
    }
}
