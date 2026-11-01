namespace Curl.Original.Protocol.Ssh;

/// <summary>
/// Pins AF-0521 (BL-4456): <see cref="SshKeptSession.Serves" /> compares the login key, which
/// holds the password and the pass phrase, in constant time and still tells an equal key from
/// one that differs in content or in length.
/// </summary>
[TestClass]
public sealed class SshKeptSessionTests
{
    private const string LoginKey = "user\0secret\0/keys/id\0/keys/id.pub\0phrase";

    [TestMethod]
    public void Serves_TheKeyTheSessionAuthenticated_IsTrue()
    {
        SshKeptSession session = SessionFor(LoginKey);

        Assert.IsTrue(session.Serves(string.Concat("user\0secret", "\0/keys/id\0/keys/id.pub\0phrase")));
    }

    [TestMethod]
    [DataRow("user\0secreT\0/keys/id\0/keys/id.pub\0phrase", DisplayName = "Password differs")]
    [DataRow("user\0secret\0/keys/id\0/keys/id.pub\0phrasE", DisplayName = "Pass phrase differs")]
    [DataRow("user\0secret\0/keys/id\0/keys/id.pub\0phrase!", DisplayName = "Longer key")]
    [DataRow("user\0secret\0/keys/id\0/keys/id.pub\0", DisplayName = "Shorter key")]
    [DataRow("", DisplayName = "Empty key")]
    public void Serves_AKeyThatDiffers_IsFalse(string key)
    {
        SshKeptSession session = SessionFor(LoginKey);

        Assert.IsFalse(session.Serves(key));
    }

    private static SshKeptSession SessionFor(string loginKey) => new(null!, null!, null!, loginKey);
}
