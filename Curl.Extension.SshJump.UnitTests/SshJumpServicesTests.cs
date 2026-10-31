namespace Curl.Extension.SshJump;

[TestClass]
public sealed class SshJumpServicesTests
{
    [TestMethod]
    [DataRow("/home/alice", null, "/home/alice/.ssh/known_hosts")]
    [DataRow("/home/alice", "/users/bob", "/home/alice/.ssh/known_hosts")]
    [DataRow(null, "/users/bob", "/users/bob/.ssh/known_hosts")]
    [DataRow(null, null, "./.ssh/known_hosts")]
    public void KnownHostsPath_HomeThenUserProfile_JoinsTheFileWithAForwardSlash(string? home, string? userProfile, string expected)
    {
        SshJumpServices services = SshJumpServices.System with
        {
            ReadEnvironmentVariable = name => name switch
            {
                "HOME" => home,
                "USERPROFILE" => userProfile,
                _ => null,
            },
        };

        Assert.AreEqual(expected, services.KnownHostsPath());
    }

    [TestMethod]
    public void System_HasTheMachinesAgent() =>
        Assert.IsNotNull(SshJumpServices.System.AgentConnector);
}
