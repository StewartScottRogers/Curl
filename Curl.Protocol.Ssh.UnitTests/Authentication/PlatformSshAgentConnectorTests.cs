using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Pins the agent order libssh2 1.11.1 walks (ADR-0304): Pageant before the OpenSSH pipe on
/// Windows, the Unix socket alone elsewhere, and no later agent once one connects - as
/// measured, curl never opened the pipe while Pageant answered.
/// </summary>
[TestClass]
public sealed class PlatformSshAgentConnectorTests
{
    [TestMethod]
    public void Create_Windows_AsksPageantBeforeThePipe()
    {
        ISshAgentConnector connector = PlatformSshAgentConnector.Create(_ => null, isWindows: true, ScriptedPageantWindow.Absent);

        FirstReachableSshAgentConnector order = (FirstReachableSshAgentConnector)connector;
        Assert.HasCount(2, order.Connectors);
        Assert.IsInstanceOfType<PageantSshAgentConnector>(order.Connectors[0]);
        Assert.IsInstanceOfType<SystemSshAgentConnector>(order.Connectors[1]);
    }

    [TestMethod]
    public void Create_ElsewhereThanWindows_UsesTheSocketAlone()
    {
        ISshAgentConnector connector = PlatformSshAgentConnector.Create(_ => null, isWindows: false, ScriptedPageantWindow.Absent);

        Assert.IsInstanceOfType<SystemSshAgentConnector>(connector);
    }

    [TestMethod]
    public async Task ConnectAsync_PageantRunningAndThePipeThere_NeverOpensThePipe()
    {
        InMemorySshAgent pageantAgent = new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "pageant-key");
        InMemorySshAgent pipe = new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "pipe-key");
        FirstReachableSshAgentConnector connector = new([new PageantSshAgentConnector(ScriptedPageantWindow.AnsweringAs(pageantAgent)), pipe]);

        await using SshAgentClient client = new((await connector.ConnectAsync(CancellationToken.None))!);
        IReadOnlyList<SshAgentIdentity>? identities = await client.RequestIdentitiesAsync(CancellationToken.None);

        Assert.AreEqual("pageant-key", identities![0].DisplayComment);
        Assert.AreEqual(0, pipe.Connections);
    }

    [TestMethod]
    public async Task ConnectAsync_NoPageantWindow_OpensThePipe()
    {
        InMemorySshAgent pipe = new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "pipe-key");
        FirstReachableSshAgentConnector connector = new([new PageantSshAgentConnector(ScriptedPageantWindow.Absent), pipe]);

        await using SshAgentClient client = new((await connector.ConnectAsync(CancellationToken.None))!);
        IReadOnlyList<SshAgentIdentity>? identities = await client.RequestIdentitiesAsync(CancellationToken.None);

        Assert.AreEqual("pipe-key", identities![0].DisplayComment);
        Assert.AreEqual(1, pipe.Connections);
    }

    [TestMethod]
    public async Task ConnectAsync_NeitherAgentThere_ReachesNoAgent()
    {
        FirstReachableSshAgentConnector connector = new([new PageantSshAgentConnector(ScriptedPageantWindow.Absent), new UnreachableSshAgent()]);

        Stream? connection = await connector.ConnectAsync(CancellationToken.None);

        Assert.IsNull(connection);
    }
}
