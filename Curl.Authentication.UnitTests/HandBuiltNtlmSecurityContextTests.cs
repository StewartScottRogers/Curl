using System.Buffers.Binary;
using System.Text;
using Curl.Kerberos;
using Curl.Ntlm;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins ADR-0142's hand-built NTLM route to what curl 8.18.0 (OpenSSL, curl's own NTLM) sent
/// on Ubuntu for <c>--ntlm -u u:p</c> on 2026-09-28 (BL-526 Notes): its Type 1 message, and
/// the Type 3 message answering MS-NLMP 4.2.4.3's CHALLENGE, byte for byte once the client
/// challenge and time it sent are injected through the seams.
/// </summary>
[TestClass]
public sealed class HandBuiltNtlmSecurityContextTests
{
    /// <summary>MS-NLMP 4.2.4.3's CHALLENGE message, flags 0xE28A8233, which the measuring server sent.</summary>
    internal const string MeasuredChallenge = "TlRMTVNTUAACAAAADAAMADgAAAAzgoriASNFZ4mrze8AAAAAAAAAACQAJABEAAAABgBwFwAAAA9TAGUAcgB2AGUAcgACAAwARABvAG0AYQBpAG4AAQAMAFMAZQByAHYAZQByAAAAAAAA";

    /// <summary>The Type 1 message curl's own NTLM sends: flags 0x00088206, no version block.</summary>
    internal const string CurlType1 = "TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=";

    /// <summary>The Type 3 message curl 8.18.0 answered <see cref="MeasuredChallenge" /> with.</summary>
    internal const string MeasuredType3 =
        "TlRMTVNTUAADAAAAGAAYAEAAAABUAFQAWAAAAAAAAACsAAAAAgACAKwAAAAWABYArgAAAAAAAAAAAAAAM4KK4iWAoKMN+kq8Eimq1kt9xV8afTTK0zM6hWRUG/gzNnfh/LfhEpJFHEgBAQAAAAAAAIAkZ3HbT90BGn00ytMzOoUAAAAAAgAMAEQAbwBtAGEAaQBuAAEADABTAGUAcgB2AGUAcgAAAAAAAAAAAHUAVwBPAFIASwBTAFQAQQBUAEkATwBOAA==";

    /// <summary>The client challenge in <see cref="MeasuredType3" />'s NTLMv2 blob.</summary>
    internal static readonly byte[] MeasuredClientChallenge = Convert.FromHexString("1A7D34CAD3333A85");

    /// <summary>The time in <see cref="MeasuredType3" />'s NTLMv2 blob, FILETIME 0x01DD4FDB71672480.</summary>
    internal static readonly DateTimeOffset MeasuredTime = DateTimeOffset.FromUnixTimeSeconds(1790663181);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task NextTokenAsync_FirstStep_GivesCurlsType1()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user", "u");
        diagnostics.Arrange("password", "p");
        using ISecurityContext context = Context(Request("u", "p"));

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        diagnostics.Act("status", step.Status);
        diagnostics.Bytes("type 1 token", step.Token);
        diagnostics.Assert("status", SecurityContextStatus.ContinueNeeded, step.Status);
        diagnostics.Diff("type 1 token base64", CurlType1, Convert.ToBase64String(step.Token));
        Assert.AreEqual(SecurityContextStatus.ContinueNeeded, step.Status);
        Assert.AreEqual(CurlType1, Convert.ToBase64String(step.Token));
        Assert.IsFalse(context.IsCompleted);
    }

    [TestMethod]
    public async Task NextTokenAsync_MeasuredChallenge_GivesTheType3Curl8180Sent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user", "u");
        diagnostics.Arrange("password", "p");
        diagnostics.Bytes("challenge", Convert.FromBase64String(MeasuredChallenge));
        using ISecurityContext context = Context(Request("u", "p"));

        SecurityContextStep step = await AnswerAsync(context, Convert.FromBase64String(MeasuredChallenge));

        diagnostics.Act("status", step.Status);
        diagnostics.Bytes("type 3 token", step.Token);
        diagnostics.Assert("status", SecurityContextStatus.Completed, step.Status);
        diagnostics.Diff("type 3 token base64", MeasuredType3, Convert.ToBase64String(step.Token));
        Assert.AreEqual(SecurityContextStatus.Completed, step.Status);
        Assert.AreEqual(MeasuredType3, Convert.ToBase64String(step.Token));
        Assert.IsTrue(context.IsCompleted);
    }

    [TestMethod]
    public async Task NextTokenAsync_CredentialWithADomain_WritesTheDomainAndUser()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user", "alice");
        diagnostics.Arrange("password", "pw");
        diagnostics.Arrange("domain", "CORP");
        using ISecurityContext context = Context(Request("alice", "pw") with { Domain = "CORP" });

        SecurityContextStep step = await AnswerAsync(context, Convert.FromBase64String(MeasuredChallenge));

        diagnostics.Bytes("type 3 token", step.Token);
        diagnostics.Act("domain", SecurityBufferText(step.Token, 28));
        diagnostics.Act("user", SecurityBufferText(step.Token, 36));
        diagnostics.Act("workstation", SecurityBufferText(step.Token, 44));
        diagnostics.Act("flags", BinaryPrimitives.ReadUInt32LittleEndian(step.Token.AsSpan(60)));
        diagnostics.Diff("domain", "CORP", SecurityBufferText(step.Token, 28));
        diagnostics.Diff("user", "alice", SecurityBufferText(step.Token, 36));
        diagnostics.Diff("workstation", NtlmAuthenticateMessage.CurlWorkstation, SecurityBufferText(step.Token, 44));
        diagnostics.Assert("flags", 0xE28A8233u, BinaryPrimitives.ReadUInt32LittleEndian(step.Token.AsSpan(60)));
        Assert.AreEqual("CORP", SecurityBufferText(step.Token, 28));
        Assert.AreEqual("alice", SecurityBufferText(step.Token, 36));
        Assert.AreEqual(NtlmAuthenticateMessage.CurlWorkstation, SecurityBufferText(step.Token, 44));
        Assert.AreEqual(0xE28A8233u, BinaryPrimitives.ReadUInt32LittleEndian(step.Token.AsSpan(60)));
    }

    [TestMethod]
    public async Task NextTokenAsync_DefaultCredentials_SendsAnEmptyUserAsCurlDoes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("credentials", "default (none supplied)");
        diagnostics.Arrange("host", "127.0.0.1");
        using ISecurityContext context = Context(new SecurityContextRequest(SecurityMechanism.Ntlm, "HTTP", "127.0.0.1"));

        SecurityContextStep step = await AnswerAsync(context, Convert.FromBase64String(MeasuredChallenge));

        diagnostics.Act("status", step.Status);
        diagnostics.Act("domain", SecurityBufferText(step.Token, 28));
        diagnostics.Act("user", SecurityBufferText(step.Token, 36));
        diagnostics.Assert("status", SecurityContextStatus.Completed, step.Status);
        diagnostics.Diff("domain", string.Empty, SecurityBufferText(step.Token, 28));
        diagnostics.Diff("user", string.Empty, SecurityBufferText(step.Token, 36));
        Assert.AreEqual(SecurityContextStatus.Completed, step.Status);
        Assert.AreEqual(string.Empty, SecurityBufferText(step.Token, 28));
        Assert.AreEqual(string.Empty, SecurityBufferText(step.Token, 36));
    }

    [TestMethod]
    public async Task NextTokenAsync_ChallengeCurlCannotRead_AnswersMalformedToken()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user", "u");
        diagnostics.Arrange("password", "p");
        diagnostics.Bytes("unreadable challenge", Convert.FromBase64String("TlRMTVNTUAACAAAA"));
        using ISecurityContext context = Context(Request("u", "p"));

        SecurityContextStep step = await AnswerAsync(context, Convert.FromBase64String("TlRMTVNTUAACAAAA"));

        diagnostics.Act("status", step.Status);
        diagnostics.Bytes("token", step.Token);
        diagnostics.Assert("status", SecurityContextStatus.MalformedToken, step.Status);
        diagnostics.Assert("token length", 0, step.Token.Length);
        Assert.AreEqual(new SecurityContextStep(SecurityContextStatus.MalformedToken, []).Status, step.Status);
        Assert.IsEmpty(step.Token);
        Assert.IsFalse(context.IsCompleted);
    }

    [TestMethod]
    public async Task NextTokenAsync_AnswerPastCurlsBuffer_AnswersRefused()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user length", 600);
        diagnostics.Arrange("password", "p");
        using ISecurityContext context = Context(Request(new string('u', 600), "p"));

        SecurityContextStep step = await AnswerAsync(context, Convert.FromBase64String(MeasuredChallenge));

        diagnostics.Act("status", step.Status);
        diagnostics.Bytes("token", step.Token);
        diagnostics.Assert("status", SecurityContextStatus.Refused, step.Status);
        diagnostics.Assert("token length", 0, step.Token.Length);
        Assert.AreEqual(SecurityContextStatus.Refused, step.Status);
        Assert.IsEmpty(step.Token);
        Assert.IsFalse(context.IsCompleted);
    }

    [TestMethod]
    public async Task NextTokenAsync_AfterCompleting_AnswersRefused()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user", "u");
        diagnostics.Arrange("password", "p");
        using ISecurityContext context = Context(Request("u", "p"));
        byte[] challenge = Convert.FromBase64String(MeasuredChallenge);
        diagnostics.Bytes("challenge", challenge);
        await AnswerAsync(context, challenge);

        SecurityContextStep step = await context.NextTokenAsync(challenge, CancellationToken.None);

        diagnostics.Act("status after completing", step.Status);
        diagnostics.Bytes("token", step.Token);
        diagnostics.Assert("status", SecurityContextStatus.Refused, step.Status);
        diagnostics.Assert("token length", 0, step.Token.Length);
        Assert.AreEqual(SecurityContextStatus.Refused, step.Status);
        Assert.IsEmpty(step.Token);
    }

    [TestMethod]
    public async Task NextTokenAsync_Cancelled_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user", "u");
        diagnostics.Arrange("password", "p");
        diagnostics.Arrange("cancellation token", "already cancelled");
        using ISecurityContext context = Context(Request("u", "p"));

        OperationCanceledException cancelled = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, new CancellationToken(canceled: true)).AsTask());

        diagnostics.Act("exception type", cancelled.GetType().Name);
        diagnostics.Assert("exception type", nameof(OperationCanceledException), cancelled.GetType().Name);
    }

    [TestMethod]
    public async Task WrapAndUnwrap_Completed_RefuseAsCurlsOwnNtlmKeepsNoSessionKey()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user", "u");
        diagnostics.Arrange("password", "p");
        diagnostics.Bytes("message", [0x01]);
        using ISecurityContext context = Context(Request("u", "p"));
        await AnswerAsync(context, Convert.FromBase64String(MeasuredChallenge));

        diagnostics.Act("is completed", context.IsCompleted);
        diagnostics.Assert("is completed", true, context.IsCompleted);
        Assert.IsTrue(context.IsCompleted);
        NotSupportedException wrapFailure = Assert.ThrowsExactly<NotSupportedException>(() => context.Wrap([0x01], encrypt: true));
        NotSupportedException unwrapFailure = Assert.ThrowsExactly<NotSupportedException>(() => context.Unwrap([0x01]));
        diagnostics.Act("wrap exception", wrapFailure.GetType().Name);
        diagnostics.Act("unwrap exception", unwrapFailure.GetType().Name);
        diagnostics.Assert("wrap exception", nameof(NotSupportedException), wrapFailure.GetType().Name);
        diagnostics.Assert("unwrap exception", nameof(NotSupportedException), unwrapFailure.GetType().Name);
    }

    private static SecurityContextRequest Request(string user, string password) =>
        new(SecurityMechanism.Ntlm, "HTTP", "127.0.0.1") { UserName = user, Password = password };

    private static HandBuiltNtlmSecurityContext Context(SecurityContextRequest request) =>
        new(request, new NtlmChallengeAnswerer(new FixedTimeProvider(MeasuredTime), new FixedNtlmRandomSource(MeasuredClientChallenge)));

    private static async Task<SecurityContextStep> AnswerAsync(ISecurityContext context, byte[] challenge)
    {
        await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        return await context.NextTokenAsync(challenge, CancellationToken.None);
    }

    /// <summary>Reads the UTF-16 string a Type 3 security buffer at <paramref name="headerOffset" /> points at (MS-NLMP 2.2.1.3).</summary>
    private static string SecurityBufferText(byte[] message, int headerOffset)
    {
        int length = BinaryPrimitives.ReadUInt16LittleEndian(message.AsSpan(headerOffset));
        int offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(message.AsSpan(headerOffset + 4));
        return Encoding.Unicode.GetString(message, offset, length);
    }
}
