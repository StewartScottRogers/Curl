using Curl.Protocol.Ssh.Fakes;
using Curl.Testing;
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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RequestIdentitiesAsync_TwoIdentities_SendsTheMeasuredRequestAndReadsBothInOrder()
    {
        byte[] second = Join(Name("ssh-rsa"), String([9]));
        ScriptedSshAgent agent = new(ScriptedSshAgent.Frames(Join([12], UInt32(2), String(Blob), Name("one"), String(second), Name("two"))));
        await using SshAgentClient client = await ConnectAsync(agent);

        IReadOnlyList<SshAgentIdentity>? identities = await client.RequestIdentitiesAsync(CancellationToken.None);

        ActIdentities(identities);
        Diagnostics.Bytes("agent request", agent.Written);
        Diagnostics.Diff("agent request", Convert.FromHexString("000000010B"), agent.Written);
        Diagnostics.Assert("identities", "2 [one, two]", Describe(identities));
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

        ActIdentities(identities);
        Diagnostics.Assert("identities", "0 []", Describe(identities));
        Assert.IsNotNull(identities);
        Assert.IsEmpty(identities);
    }

    [TestMethod]
    public async Task RequestIdentitiesAsync_AnswerLongerThanOneRead_ReadsItWhole()
    {
        byte[] comment = new byte[100_000];
        Array.Fill(comment, (byte)'c');
        Diagnostics.Arrange("comment length", comment.Length);
        await using SshAgentClient client = await ConnectAsync(new ScriptedSshAgent(ScriptedSshAgent.Frames(Join([12], UInt32(1), String(Blob), String(comment)))));

        IReadOnlyList<SshAgentIdentity>? identities = await client.RequestIdentitiesAsync(CancellationToken.None);

        Diagnostics.Act("identities", identities?.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null");
        if (identities is { Count: > 0 })
        {
            Diagnostics.Diff("comment", comment, identities[0].Comment);
        }

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

        Assert.IsNull(await RequestIdentitiesExpectingNullAsync(client));
    }

    [TestMethod]
    [DataRow("", DisplayName = "the agent closes at once")]
    [DataRow("0000", DisplayName = "the length cut short")]
    [DataRow("000000050C00", DisplayName = "the body cut short")]
    public async Task RequestIdentitiesAsync_AgentClosesEarly_ReturnsNull(string outputHex)
    {
        await using SshAgentClient client = await ConnectAsync(new ScriptedSshAgent(Convert.FromHexString(outputHex)));

        Assert.IsNull(await RequestIdentitiesExpectingNullAsync(client));
    }

    [TestMethod]
    public async Task RequestIdentitiesAsync_AgentCannotBeWrittenTo_ReturnsNull()
    {
        Diagnostics.Arrange("agent", "fails every write");
        await using SshAgentClient client = await ConnectAsync(new ScriptedSshAgent([]) { FailsWrites = true });

        Assert.IsNull(await RequestIdentitiesExpectingNullAsync(client));
    }

    [TestMethod]
    public async Task SignAsync_Answered_SendsTheKeyTheDataAndTheFlagsAndReadsTheMethodAndSignature()
    {
        ScriptedSshAgent agent = new(ScriptedSshAgent.Frames(Join([14], String(Join(Name("rsa-sha2-512"), String([5, 6]))))));
        await using SshAgentClient client = await ConnectAsync(agent);
        Diagnostics.Arrange("sign", "key blob, data 07 08, flags 4");

        SshAgentSignature? signature = await client.SignAsync(Blob, [7, 8], 4, CancellationToken.None);

        byte[] request = Join([13], String(Blob), String([7, 8]), UInt32(4));
        ActSignature(signature);
        Diagnostics.Bytes("agent request", agent.Written);
        Diagnostics.Diff("agent request", Join(UInt32((uint)request.Length), request), agent.Written);
        Diagnostics.Assert("signature method", "rsa-sha2-512", MethodOf(signature));
        CollectionAssert.AreEqual(Join(UInt32((uint)request.Length), request), agent.Written);
        Assert.IsNotNull(signature);
        CollectionAssert.AreEqual("rsa-sha2-512"u8.ToArray(), signature.Method);
        CollectionAssert.AreEqual(new byte[] { 5, 6 }, signature.Signature);
    }

    [TestMethod]
    public async Task SignAsync_OuterLengthWrong_SkipsItUnreadAsLibssh2Does()
    {
        Diagnostics.Arrange("answer", "SIGN_RESPONSE whose outer length says 999");
        await using SshAgentClient client = await ConnectAsync(new ScriptedSshAgent(ScriptedSshAgent.Frames(Join([14], UInt32(999), Name("ssh-ed25519"), String([5])))));

        SshAgentSignature? signature = await client.SignAsync(Blob, [], 0, CancellationToken.None);

        ActSignature(signature);
        Diagnostics.Diff("signature", new byte[] { 5 }, signature?.Signature ?? []);
        CollectionAssert.AreEqual(new byte[] { 5 }, signature?.Signature);
    }

    [TestMethod]
    public async Task SignAsync_SignatureMissing_ReadsTheMethodAlone()
    {
        Diagnostics.Arrange("answer", "SIGN_RESPONSE with a method and no signature");
        await using SshAgentClient client = await ConnectAsync(new ScriptedSshAgent(ScriptedSshAgent.Frames(Join([14], UInt32(15), Name("ssh-ed25519")))));

        SshAgentSignature? signature = await client.SignAsync(Blob, [], 0, CancellationToken.None);

        ActSignature(signature);
        Diagnostics.Assert("signature method", "ssh-ed25519", MethodOf(signature));
        Diagnostics.Assert("signature present", false, signature?.Signature is not null);
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

        Assert.IsNull(await SignExpectingNullAsync(client));
    }

    [TestMethod]
    public async Task SignAsync_AgentGone_ReturnsNull()
    {
        Diagnostics.Arrange("agent", "closes before answering");
        await using SshAgentClient client = await ConnectAsync(new ScriptedSshAgent([]));

        Assert.IsNull(await SignExpectingNullAsync(client));
    }

    [TestMethod]
    public async Task DisposeAsync_ClosesTheConnection()
    {
        ScriptedSshAgent agent = new([]);
        SshAgentClient client = await ConnectAsync(agent);
        Diagnostics.Arrange("agent", "connected, answering nothing");

        await client.DisposeAsync();

        Diagnostics.Act("connection disposed", agent.WasDisposed);
        Diagnostics.Assert("connection disposed", true, agent.WasDisposed);
        Assert.IsTrue(agent.WasDisposed);
    }

    private static string Describe(IReadOnlyList<SshAgentIdentity>? identities) =>
        identities is null ? "null" : $"{identities.Count} [{string.Join(", ", identities.Select(identity => SshAuthenticationDiagnostics.Text(identity.DisplayComment)))}]";

    private static string MethodOf(SshAgentSignature? signature) =>
        signature is null ? "null" : System.Text.Encoding.ASCII.GetString(signature.Method);

    private void ActIdentities(IReadOnlyList<SshAgentIdentity>? identities)
    {
        Diagnostics.Act("identities", Describe(identities));
        for (int index = 0; identities is not null && index < identities.Count; index++)
        {
            Diagnostics.Bytes($"identity {index} blob", identities[index].Blob);
        }
    }

    private void ActSignature(SshAgentSignature? signature)
    {
        Diagnostics.Act("signature method", MethodOf(signature));
        if (signature?.Signature is not null)
        {
            Diagnostics.Bytes("signature", signature.Signature);
        }
    }

    // Lists the identities of an agent whose answer libssh2 cannot read, writing what it got.
    private async Task<IReadOnlyList<SshAgentIdentity>?> RequestIdentitiesExpectingNullAsync(SshAgentClient client)
    {
        IReadOnlyList<SshAgentIdentity>? identities = await client.RequestIdentitiesAsync(CancellationToken.None);
        Diagnostics.Act("identities", Describe(identities));
        Diagnostics.Assert("identities", "null", Describe(identities));
        return identities;
    }

    // Signs through an agent whose answer libssh2 cannot read, writing what it got.
    private async Task<SshAgentSignature?> SignExpectingNullAsync(SshAgentClient client)
    {
        SshAgentSignature? signature = await client.SignAsync(Blob, [], 0, CancellationToken.None);
        Diagnostics.Act("signature method", MethodOf(signature));
        Diagnostics.Assert("signature", "null", MethodOf(signature));
        return signature;
    }

    private async Task<SshAgentClient> ConnectAsync(ScriptedSshAgent agent)
    {
        Diagnostics.Arrange("agent output length", agent.Output.Length);
        Diagnostics.Bytes("agent output", agent.Output);
        return new((await agent.ConnectAsync(CancellationToken.None))!);
    }
}
