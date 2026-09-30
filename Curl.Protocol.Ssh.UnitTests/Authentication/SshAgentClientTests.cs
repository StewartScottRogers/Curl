using Curl.Protocol.Ssh.Fakes;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Pins the agent protocol <see cref="SshAgentClient" /> speaks: the request bytes curl sent
/// OpenSSH's <c>ssh-agent</c> in the 2026-09-30 measurement (BL-902), and libssh2 1.11.1's
/// reading of each answer, where anything it cannot read fails the request whole.
/// </summary>
[TestClass]
public sealed class SshAgentClientTests
{
    private static readonly byte[] Blob = Join(Name("ssh-ed25519"), String([1, 2, 3]));

    [TestMethod]
    public async Task RequestIdentitiesAsync_TwoIdentities_SendsTheMeasuredRequestAndReadsBothInOrder()
    {
        byte[] second = Join(Name("ssh-rsa"), String([9]));
        ScriptedSshAgent agent = new(ScriptedSshAgent.Frames(Join([12], UInt32(2), String(Blob), Name("one"), String(second), Name("two"))));
        await using SshAgentClient client = await ConnectAsync(agent);

        IReadOnlyList<SshAgentIdentity>? identities = await client.RequestIdentitiesAsync(CancellationToken.None);

        CollectionAssert.AreEqual(Convert.FromHexString("000000010B"), agent.Written);
        Assert.IsNotNull(identities);
        Assert.HasCount(2, identities);
        CollectionAssert.AreEqual(Blob, identities[0].Blob);
        Assert.AreEqual("one", identities[0].DisplayComment);
        CollectionAssert.AreEqual(second, identities[1].Blob);
        Assert.AreEqual("two", identities[1].DisplayComment);
    }

    [TestMethod]
    public async Task RequestIdentitiesAsync_NoIdentity_ReadsAnEmptyListAsMeasured()
    {
        await using SshAgentClient client = await ConnectAsync(new ScriptedSshAgent(Convert.FromHexString("000000050C00000000")));

        IReadOnlyList<SshAgentIdentity>? identities = await client.RequestIdentitiesAsync(CancellationToken.None);

        Assert.IsNotNull(identities);
        Assert.IsEmpty(identities);
    }

    [TestMethod]
    public async Task RequestIdentitiesAsync_AnswerLongerThanOneRead_ReadsItWhole()
    {
        byte[] comment = new byte[100_000];
        Array.Fill(comment, (byte)'c');
        await using SshAgentClient client = await ConnectAsync(new ScriptedSshAgent(ScriptedSshAgent.Frames(Join([12], UInt32(1), String(Blob), String(comment)))));

        IReadOnlyList<SshAgentIdentity>? identities = await client.RequestIdentitiesAsync(CancellationToken.None);

        Assert.IsNotNull(identities);
        CollectionAssert.AreEqual(comment, identities[0].Comment);
    }

    [TestMethod]
    [DataRow("", DisplayName = "an empty answer")]
    [DataRow("05", DisplayName = "SSH_AGENT_FAILURE")]
    [DataRow("0C", DisplayName = "no count")]
    [DataRow("0C00000001", DisplayName = "the count names an identity that is not there")]
    [DataRow("0C000000010000000501", DisplayName = "a blob cut short")]
    [DataRow("0C00000001000000010100000005616263", DisplayName = "a comment cut short")]
    public async Task RequestIdentitiesAsync_AnswerLibssh2CannotRead_ReturnsNull(string answerHex)
    {
        await using SshAgentClient client = await ConnectAsync(new ScriptedSshAgent(ScriptedSshAgent.Frames(Convert.FromHexString(answerHex))));

        Assert.IsNull(await client.RequestIdentitiesAsync(CancellationToken.None));
    }

    [TestMethod]
    [DataRow("", DisplayName = "the agent closes at once")]
    [DataRow("0000", DisplayName = "the length cut short")]
    [DataRow("000000050C00", DisplayName = "the body cut short")]
    public async Task RequestIdentitiesAsync_AgentClosesEarly_ReturnsNull(string outputHex)
    {
        await using SshAgentClient client = await ConnectAsync(new ScriptedSshAgent(Convert.FromHexString(outputHex)));

        Assert.IsNull(await client.RequestIdentitiesAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task RequestIdentitiesAsync_AgentCannotBeWrittenTo_ReturnsNull()
    {
        await using SshAgentClient client = await ConnectAsync(new ScriptedSshAgent([]) { FailsWrites = true });

        Assert.IsNull(await client.RequestIdentitiesAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task SignAsync_Answered_SendsTheKeyTheDataAndTheFlagsAndReadsTheMethodAndSignature()
    {
        ScriptedSshAgent agent = new(ScriptedSshAgent.Frames(Join([14], String(Join(Name("rsa-sha2-512"), String([5, 6]))))));
        await using SshAgentClient client = await ConnectAsync(agent);

        SshAgentSignature? signature = await client.SignAsync(Blob, [7, 8], 4, CancellationToken.None);

        byte[] request = Join([13], String(Blob), String([7, 8]), UInt32(4));
        CollectionAssert.AreEqual(Join(UInt32((uint)request.Length), request), agent.Written);
        Assert.IsNotNull(signature);
        CollectionAssert.AreEqual("rsa-sha2-512"u8.ToArray(), signature.Method);
        CollectionAssert.AreEqual(new byte[] { 5, 6 }, signature.Signature);
    }

    [TestMethod]
    public async Task SignAsync_OuterLengthWrong_SkipsItUnreadAsLibssh2Does()
    {
        await using SshAgentClient client = await ConnectAsync(new ScriptedSshAgent(ScriptedSshAgent.Frames(Join([14], UInt32(999), Name("ssh-ed25519"), String([5])))));

        SshAgentSignature? signature = await client.SignAsync(Blob, [], 0, CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 5 }, signature?.Signature);
    }

    [TestMethod]
    public async Task SignAsync_SignatureMissing_ReadsTheMethodAlone()
    {
        await using SshAgentClient client = await ConnectAsync(new ScriptedSshAgent(ScriptedSshAgent.Frames(Join([14], UInt32(15), Name("ssh-ed25519")))));

        SshAgentSignature? signature = await client.SignAsync(Blob, [], 0, CancellationToken.None);

        Assert.IsNotNull(signature);
        CollectionAssert.AreEqual("ssh-ed25519"u8.ToArray(), signature.Method);
        Assert.IsNull(signature.Signature);
    }

    [TestMethod]
    [DataRow("", DisplayName = "an empty answer")]
    [DataRow("05", DisplayName = "SSH_AGENT_FAILURE")]
    [DataRow("0E000000", DisplayName = "the outer length cut short")]
    [DataRow("0E0000000000000009", DisplayName = "the method cut short")]
    public async Task SignAsync_AnswerLibssh2CannotRead_ReturnsNull(string answerHex)
    {
        await using SshAgentClient client = await ConnectAsync(new ScriptedSshAgent(ScriptedSshAgent.Frames(Convert.FromHexString(answerHex))));

        Assert.IsNull(await client.SignAsync(Blob, [], 0, CancellationToken.None));
    }

    [TestMethod]
    public async Task SignAsync_AgentGone_ReturnsNull()
    {
        await using SshAgentClient client = await ConnectAsync(new ScriptedSshAgent([]));

        Assert.IsNull(await client.SignAsync(Blob, [], 0, CancellationToken.None));
    }

    [TestMethod]
    public async Task DisposeAsync_ClosesTheConnection()
    {
        ScriptedSshAgent agent = new([]);
        SshAgentClient client = await ConnectAsync(agent);

        await client.DisposeAsync();

        Assert.IsTrue(agent.WasDisposed);
    }

    private static async Task<SshAgentClient> ConnectAsync(ScriptedSshAgent agent) => new((await agent.ConnectAsync(CancellationToken.None))!);
}
