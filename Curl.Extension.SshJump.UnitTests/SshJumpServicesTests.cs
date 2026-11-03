using Curl.Ssh.Authentication;

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
    public void System_HasTheMachinesAgent()
    {
        ISshAgentConnector? connector = SshJumpServices.System.AgentConnector;

        if (OperatingSystem.IsWindows())
        {
            Assert.IsInstanceOfType<FirstReachableSshAgentConnector>(connector);
            FirstReachableSshAgentConnector order = (FirstReachableSshAgentConnector)connector!;
            Assert.HasCount(2, order.Connectors);
            Assert.IsInstanceOfType<PageantSshAgentConnector>(order.Connectors[0]);
            Assert.IsInstanceOfType<SystemSshAgentConnector>(order.Connectors[1]);
        }
        else
        {
            Assert.IsInstanceOfType<SystemSshAgentConnector>(connector);
        }
    }
}
