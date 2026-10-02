using System.Buffers.Binary;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Pins <see cref="PageantSshAgentConnector" /> against a scripted Pageant window: libssh2
/// 1.11.1's <c>agent_connect_pageant</c> and <c>agent_transact_pageant</c>, and the
/// 2026-10-01 measurement (ADR-0304) that a message answered with zero is
/// <c>failure requesting identities to agent</c>.
/// </summary>
[TestClass]
public sealed class PageantSshAgentConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_NoPageantWindow_ReachesNoAgent()
    {
        PageantSshAgentConnector connector = new(ScriptedPageantWindow.Absent);

        Stream? connection = await connector.ConnectAsync(CancellationToken.None);

        Assert.IsNull(connection);
    }

    [TestMethod]
    public async Task ConnectAsync_PageantHoldsAKey_ListsAndSignsThroughTheMapping()
    {
        InMemorySshAgent agent = new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "pageant-key");
        ScriptedPageantWindow window = ScriptedPageantWindow.AnsweringAs(agent);
        await using SshAgentClient client = new((await new PageantSshAgentConnector(window).ConnectAsync(CancellationToken.None))!);

        IReadOnlyList<SshAgentIdentity>? identities = await client.RequestIdentitiesAsync(CancellationToken.None);
        SshAgentSignature? signature = await client.SignAsync(identities![0].Blob, [1, 2, 3], 0, CancellationToken.None);

        Assert.AreEqual("pageant-key", identities[0].DisplayComment);
        Assert.IsNotNull(signature);
        Assert.AreEqual(2, window.Exchanges);
        CollectionAssert.AreEqual(new byte[] { 11 }, agent.Requests[0]);
    }

    [TestMethod]
    public async Task ConnectAsync_MessageReturnsZero_TheListFailsAsMeasured()
    {
        await using SshAgentClient client = new((await new PageantSshAgentConnector(new ScriptedPageantWindow(_ => false, true)).ConnectAsync(CancellationToken.None))!);

        IReadOnlyList<SshAgentIdentity>? identities = await client.RequestIdentitiesAsync(CancellationToken.None);

        Assert.IsNull(identities);
    }

    [TestMethod]
    public void Transact_WindowGoneSinceTheConnection_SendsNothing()
    {
        ScriptedPageantWindow window = new(_ => true, true, false);
        PageantSshAgentConnector connector = new(window);
        window.IsRunning();

        byte[] answer = connector.Transact([0, 0, 0, 1, 11]);

        Assert.IsEmpty(answer);
        Assert.AreEqual(0, window.Exchanges);
    }

    [TestMethod]
    public void Transact_FrameLongerThanTheMapping_SendsNothing()
    {
        ScriptedPageantWindow window = new(_ => true, true);
        byte[] frame = new byte[PageantSshAgentConnector.MaximumMessageLength + 1];
        BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)(frame.Length - 4));

        byte[] answer = new PageantSshAgentConnector(window).Transact(frame);

        Assert.IsEmpty(answer);
        Assert.AreEqual(0, window.Exchanges);
    }

    [TestMethod]
    public void Transact_FrameFillingTheMapping_IsSent()
    {
        ScriptedPageantWindow window = new(mapping => ScriptedPageantWindow.AnswerInPlace(mapping, _ => [6]), true);
        byte[] frame = new byte[PageantSshAgentConnector.MaximumMessageLength];
        BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)(frame.Length - 4));

        byte[] answer = new PageantSshAgentConnector(window).Transact(frame);

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 1, 6 }, answer);
    }

    [TestMethod]
    [DataRow(PageantSshAgentConnector.MaximumMessageLength - 4, true, DisplayName = "fills the mapping")]
    [DataRow(PageantSshAgentConnector.MaximumMessageLength - 3, false, DisplayName = "overruns the mapping")]
    public void Transact_AnswerLength_IsReadOnlyWhenItFitsTheMapping(int length, bool read)
    {
        ScriptedPageantWindow window = new(
            mapping =>
            {
                BinaryPrimitives.WriteUInt32BigEndian(mapping, (uint)length);
                return true;
            },
            true);

        byte[] answer = new PageantSshAgentConnector(window).Transact([0, 0, 0, 1, 11]);

        Assert.AreEqual(read ? length + 4 : 0, answer.Length);
    }
}
