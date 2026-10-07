using Curl.Protocol.Ssh.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Pins the agent order libssh2 1.11.1 walks (ADR-0304): Pageant before the OpenSSH pipe on
/// Windows, the Unix socket alone elsewhere, and no later agent once one connects - as
/// measured, curl never opened the pipe while Pageant answered.
/// </summary>
[TestClass]
public sealed class PlatformSshAgentConnectorTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Create_Windows_AsksPageantBeforeThePipe()
    {
        Diagnostics.Arrange("platform", "Windows");

        ISshAgentConnector connector = PlatformSshAgentConnector.Create(_ => null, isWindows: true, ScriptedPageantWindow.Absent);

        FirstReachableSshAgentConnector order = (FirstReachableSshAgentConnector)connector;
        string names = string.Join(", ", order.Connectors.Select(each => each.GetType().Name));
        Diagnostics.Act("connectors", names);
        Diagnostics.Assert("connectors", $"{nameof(PageantSshAgentConnector)}, {nameof(SystemSshAgentConnector)}", names);
        Assert.HasCount(2, order.Connectors);
        Assert.IsInstanceOfType<PageantSshAgentConnector>(order.Connectors[0]);
        Assert.IsInstanceOfType<SystemSshAgentConnector>(order.Connectors[1]);
    }

    [TestMethod]
    public void Create_ElsewhereThanWindows_UsesTheSocketAlone()
    {
        Diagnostics.Arrange("platform", "not Windows");

        ISshAgentConnector connector = PlatformSshAgentConnector.Create(_ => null, isWindows: false, ScriptedPageantWindow.Absent);

        Diagnostics.Act("connector", connector.GetType().Name);
        Diagnostics.Assert("connector", nameof(SystemSshAgentConnector), connector.GetType().Name);
        Assert.IsInstanceOfType<SystemSshAgentConnector>(connector);
    }

    [TestMethod]
    public async Task ConnectAsync_PageantRunningAndThePipeThere_NeverOpensThePipe()
    {
        InMemorySshAgent pageantAgent = new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "pageant-key");
        InMemorySshAgent pipe = new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "pipe-key");
        FirstReachableSshAgentConnector connector = new([new PageantSshAgentConnector(ScriptedPageantWindow.AnsweringAs(pageantAgent)), pipe]);
        Diagnostics.Arrange("agents", "Pageant (pageant-key), then the pipe (pipe-key)");

        await using SshAgentClient client = new((await connector.ConnectAsync(CancellationToken.None))!);
        IReadOnlyList<SshAgentIdentity>? identities = await client.RequestIdentitiesAsync(CancellationToken.None);

        Diagnostics.Act("identity comment", identities![0].DisplayComment);
        Diagnostics.Act("pipe connections", pipe.Connections);
        Diagnostics.Assert("identity comment", "pageant-key", identities[0].DisplayComment);
        Diagnostics.Assert("pipe connections", 0, pipe.Connections);
        Assert.AreEqual("pageant-key", identities![0].DisplayComment);
        Assert.AreEqual(0, pipe.Connections);
    }

    [TestMethod]
    public async Task ConnectAsync_NoPageantWindow_OpensThePipe()
    {
        InMemorySshAgent pipe = new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "pipe-key");
        FirstReachableSshAgentConnector connector = new([new PageantSshAgentConnector(ScriptedPageantWindow.Absent), pipe]);
        Diagnostics.Arrange("agents", "Pageant absent, then the pipe (pipe-key)");

        await using SshAgentClient client = new((await connector.ConnectAsync(CancellationToken.None))!);
        IReadOnlyList<SshAgentIdentity>? identities = await client.RequestIdentitiesAsync(CancellationToken.None);

        Diagnostics.Act("identity comment", identities![0].DisplayComment);
        Diagnostics.Act("pipe connections", pipe.Connections);
        Diagnostics.Assert("identity comment", "pipe-key", identities[0].DisplayComment);
        Diagnostics.Assert("pipe connections", 1, pipe.Connections);
        Assert.AreEqual("pipe-key", identities![0].DisplayComment);
        Assert.AreEqual(1, pipe.Connections);
    }

    [TestMethod]
    public async Task ConnectAsync_NeitherAgentThere_ReachesNoAgent()
    {
        FirstReachableSshAgentConnector connector = new([new PageantSshAgentConnector(ScriptedPageantWindow.Absent), new UnreachableSshAgent()]);
        Diagnostics.Arrange("agents", "Pageant absent, then an unreachable agent");

        Stream? connection = await connector.ConnectAsync(CancellationToken.None);

        string described = connection is null ? "none" : connection.GetType().Name;
        Diagnostics.Act("connection", described);
        Diagnostics.Assert("connection", "none", described);
        Assert.IsNull(connection);
    }
}
