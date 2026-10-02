using System.Runtime.Versioning;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Runs <see cref="WindowsPageantWindow" />'s Win32 calls against a window of class and
/// title <c>Pageant</c> served here (ADR-0083: the adapter is excluded from the fast run's
/// coverage and measured by these Integration tests). One window at a time, so the class
/// runs alone.
/// </summary>
[TestClass]
[DoNotParallelize]
[SupportedOSPlatform("windows")]
public sealed class WindowsPageantWindowTests
{
    [TestMethod]
    [TestCategory("Integration")]
    [OSCondition(OperatingSystems.Windows)]
    public async Task Exchange_PageantWindowServed_ListsItsIdentitiesThroughANamedMapping()
    {
        InMemorySshAgent agent = new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "pageant-key");
        using FakePageantWindow window = new(agent.Answer);
        Stream? connection = await new PageantSshAgentConnector(new WindowsPageantWindow()).ConnectAsync(CancellationToken.None);
        Assert.IsNotNull(connection);
        await using SshAgentClient client = new(connection);

        IReadOnlyList<SshAgentIdentity>? identities = await client.RequestIdentitiesAsync(CancellationToken.None);

        Assert.AreEqual("pageant-key", identities![0].DisplayComment);
        Assert.HasCount(1, window.MapNames);
        StringAssert.Matches(window.MapNames[0], new System.Text.RegularExpressions.Regex("^PageantRequest[0-9a-f]{8}$"));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [OSCondition(OperatingSystems.Windows)]
    public void Exchange_MessageReturnsZero_Fails()
    {
        using FakePageantWindow window = new(_ => null);

        bool exchanged = new WindowsPageantWindow().Exchange(new byte[PageantSshAgentConnector.MaximumMessageLength]);

        Assert.IsFalse(exchanged);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [OSCondition(OperatingSystems.Windows)]
    public void IsRunning_NoPageantWindow_IsFalse()
    {
        WindowsPageantWindow window = new();

        bool running = window.IsRunning();
        bool exchanged = window.Exchange(new byte[PageantSshAgentConnector.MaximumMessageLength]);

        Assert.IsFalse(running);
        Assert.IsFalse(exchanged);
    }
}
