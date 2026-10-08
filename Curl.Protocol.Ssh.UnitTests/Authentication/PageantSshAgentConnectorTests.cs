using System.Buffers.Binary;
using Curl.Protocol.Ssh.Fakes;
using Curl.Testing;

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
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ConnectAsync_NoPageantWindow_ReachesNoAgent()
    {
        PageantSshAgentConnector connector = new(ScriptedPageantWindow.Absent);
        Diagnostics.Arrange("Pageant window", "absent");

        Stream? connection = await connector.ConnectAsync(CancellationToken.None);

        Diagnostics.Act("connection", Describe(connection));
        Diagnostics.Assert("connection", "none", Describe(connection));
        Assert.IsNull(connection);
    }

    [TestMethod]
    public async Task ConnectAsync_PageantHoldsAKey_ListsAndSignsThroughTheMapping()
    {
        InMemorySshAgent agent = new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "pageant-key");
        ScriptedPageantWindow window = ScriptedPageantWindow.AnsweringAs(agent);
        Diagnostics.Arrange("Pageant agent", "one RSA key, comment pageant-key");
        await using SshAgentClient client = new((await new PageantSshAgentConnector(window).ConnectAsync(CancellationToken.None))!);

        IReadOnlyList<SshAgentIdentity>? identities = await client.RequestIdentitiesAsync(CancellationToken.None);
        SshAgentSignature? signature = await client.SignAsync(identities![0].Blob, [1, 2, 3], 0, CancellationToken.None);

        Diagnostics.Act("identity comment", identities[0].DisplayComment);
        Diagnostics.Act("signed", signature is not null);
        Diagnostics.Act("window exchanges", window.Exchanges);
        Diagnostics.Bytes("first agent request", agent.Requests[0]);
        Diagnostics.Assert("identity comment", "pageant-key", identities[0].DisplayComment);
        Diagnostics.Assert("window exchanges", 2, window.Exchanges);
        Diagnostics.Diff("first agent request", new byte[] { 11 }, agent.Requests[0]);
        Assert.AreEqual("pageant-key", identities[0].DisplayComment);
        Assert.IsNotNull(signature);
        Assert.AreEqual(2, window.Exchanges);
        CollectionAssert.AreEqual(new byte[] { 11 }, agent.Requests[0]);
    }

    [TestMethod]
    public async Task ConnectAsync_MessageReturnsZero_TheListFailsAsMeasured()
    {
        Diagnostics.Arrange("Pageant window", "running; every message returns zero");
        await using SshAgentClient client = new((await new PageantSshAgentConnector(new ScriptedPageantWindow(_ => false, true)).ConnectAsync(CancellationToken.None))!);

        IReadOnlyList<SshAgentIdentity>? identities = await client.RequestIdentitiesAsync(CancellationToken.None);

        Diagnostics.Act("identities", identities?.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null");
        Diagnostics.Assert("identities", "null", identities?.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null");
        Assert.IsNull(identities);
    }

    [TestMethod]
    public void Transact_WindowGoneSinceTheConnection_SendsNothing()
    {
        ScriptedPageantWindow window = new(_ => true, true, false);
        PageantSshAgentConnector connector = new(window);
        window.IsRunning();
        Diagnostics.Arrange("Pageant window", "running at the connection, gone since");

        byte[] answer = connector.Transact([0, 0, 0, 1, 11]);

        Diagnostics.Bytes("answer", answer);
        Diagnostics.Act("window exchanges", window.Exchanges);
        Diagnostics.Assert("answer length", 0, answer.Length);
        Diagnostics.Assert("window exchanges", 0, window.Exchanges);
        Assert.IsEmpty(answer);
        Assert.AreEqual(0, window.Exchanges);
    }

    [TestMethod]
    public void Transact_FrameLongerThanTheMapping_SendsNothing()
    {
        ScriptedPageantWindow window = new(_ => true, true);
        byte[] frame = new byte[PageantSshAgentConnector.MaximumMessageLength + 1];
        BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)(frame.Length - 4));
        Diagnostics.Arrange("frame length", frame.Length);

        byte[] answer = new PageantSshAgentConnector(window).Transact(frame);

        Diagnostics.Act("answer length", answer.Length);
        Diagnostics.Act("window exchanges", window.Exchanges);
        Diagnostics.Assert("answer length", 0, answer.Length);
        Diagnostics.Assert("window exchanges", 0, window.Exchanges);
        Assert.IsEmpty(answer);
        Assert.AreEqual(0, window.Exchanges);
    }

    [TestMethod]
    public void Transact_FrameFillingTheMapping_IsSent()
    {
        ScriptedPageantWindow window = new(mapping => ScriptedPageantWindow.AnswerInPlace(mapping, _ => [6]), true);
        byte[] frame = new byte[PageantSshAgentConnector.MaximumMessageLength];
        BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)(frame.Length - 4));
        Diagnostics.Arrange("frame length", frame.Length);

        byte[] answer = new PageantSshAgentConnector(window).Transact(frame);

        Diagnostics.Act("answer length", answer.Length);
        Diagnostics.Bytes("answer", answer);
        Diagnostics.Diff("answer", new byte[] { 0, 0, 0, 1, 6 }, answer);
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
        Diagnostics.Arrange("answer length field", length);
        Diagnostics.Arrange("mapping size", PageantSshAgentConnector.MaximumMessageLength);

        byte[] answer = new PageantSshAgentConnector(window).Transact([0, 0, 0, 1, 11]);

        Diagnostics.Act("answer length", answer.Length);
        Diagnostics.Assert("answer length", read ? length + 4 : 0, answer.Length);
        Assert.AreEqual(read ? length + 4 : 0, answer.Length);
    }

    private static string Describe(Stream? connection) => connection is null ? "none" : connection.GetType().Name;
}
