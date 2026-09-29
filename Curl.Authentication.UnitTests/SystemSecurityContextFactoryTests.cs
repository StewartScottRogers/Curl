using System.Buffers;
using System.Net;
using System.Net.Security;
using System.Security.Principal;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="SystemSecurityContextFactory" />: the <see cref="NegotiateAuthentication" />
/// options each request makes, and what SSPI and the system GSS-API answer as ADR-0142
/// measured them: SSPI's NTLM Type 1 for <c>-u u:p</c> starts as curl 8.21.0's does, and off
/// Windows without <c>gss-ntlmssp</c> NTLM is unsupported, which the router falls back on.
/// </summary>
[TestClass]
public sealed class SystemSecurityContextFactoryTests
{
    // The first 16 bytes of the Type 1 curl 8.21.0 (Schannel, SSPI) sent for --ntlm -u u:p:
    // "NTLMSSP\0", type 1, flags 0xA2088207 (ADR-0142). The OS version block after them varies by build.
    private const string MeasuredType1Start = "4E544C4D5353500001000000078208A2";

    [TestMethod]
    [DataRow(SecurityMechanism.Ntlm, "NTLM")]
    [DataRow(SecurityMechanism.Negotiate, "Negotiate")]
    [DataRow(SecurityMechanism.Kerberos, "Kerberos")]
    public void OptionsFor_Mechanism_NamesItsPackageAndTheServiceSlashHostTarget(SecurityMechanism mechanism, string package)
    {
        NegotiateAuthenticationClientOptions options = SystemSecurityContextFactory.OptionsFor(new SecurityContextRequest(mechanism, "HTTP", "server.example.test"));

        Assert.AreEqual(package, options.Package);
        Assert.AreEqual("HTTP/server.example.test", options.TargetName);
        Assert.AreSame(CredentialCache.DefaultNetworkCredentials, options.Credential);
        Assert.AreEqual(TokenImpersonationLevel.None, options.AllowedImpersonationLevel);
        Assert.AreEqual(ProtectionLevel.None, options.RequiredProtectionLevel);
    }

    [TestMethod]
    public void OptionsFor_ExplicitCredentialAndAlwaysDelegation_PassesBoth()
    {
        SecurityContextRequest request = new(SecurityMechanism.Negotiate, "HTTP", "h") { UserName = "alice", Password = "pw", Domain = "EXAMPLE", Delegation = SecurityDelegation.Always };

        NegotiateAuthenticationClientOptions options = SystemSecurityContextFactory.OptionsFor(request);

        Assert.AreEqual("alice", options.Credential.UserName);
        Assert.AreEqual("pw", options.Credential.Password);
        Assert.AreEqual("EXAMPLE", options.Credential.Domain);
        Assert.AreEqual(TokenImpersonationLevel.Delegation, options.AllowedImpersonationLevel);
    }

    [TestMethod]
    public void OptionsFor_PolicyDelegation_AsksNoneForWantOfAPolicyFlag()
    {
        SecurityContextRequest request = new(SecurityMechanism.Negotiate, "HTTP", "h") { Delegation = SecurityDelegation.Policy };

        Assert.AreEqual(TokenImpersonationLevel.None, SystemSecurityContextFactory.OptionsFor(request).AllowedImpersonationLevel);
    }

    [TestMethod]
    public void OptionsFor_OtherServiceName_NamesItsServiceSlashHostTarget()
    {
        NegotiateAuthenticationClientOptions options = SystemSecurityContextFactory.OptionsFor(new SecurityContextRequest(SecurityMechanism.Negotiate, "svc", "proxy.example.test"));

        Assert.AreEqual("svc/proxy.example.test", options.TargetName);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task NextTokenAsync_WindowsNtlmExplicitCredential_GivesSspisType1()
    {
        using ISecurityContext context = new SystemSecurityContextFactory().Create(Ntlm());

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.AreEqual(SecurityContextStatus.ContinueNeeded, step.Status);
        Assert.AreEqual(MeasuredType1Start, Convert.ToHexString(step.Token, 0, 16));
        Assert.IsFalse(context.IsCompleted);
    }

    /// <summary>
    /// curl 8.21.0 (Schannel, SSPI) answered MS-NLMP 4.2.4.3's CHALLENGE with a Type 3 whose
    /// header starts <c>NTLMSSP\0</c>, type 3, a 24-byte LM response, and carries flags
    /// 0xA2888205 and user <c>u</c> (BL-526 Notes); its workstation is the machine's name
    /// and its NTLMv2 response holds a fresh nonce, time and MIC, so only these are pinned.
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task NextTokenAsync_WindowsNtlmMeasuredChallenge_GivesSspisType3()
    {
        using ISecurityContext context = new SystemSecurityContextFactory().Create(Ntlm());
        await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        SecurityContextStep step = await context.NextTokenAsync(Convert.FromBase64String(HandBuiltNtlmSecurityContextTests.MeasuredChallenge), CancellationToken.None);

        Assert.AreEqual(SecurityContextStatus.Completed, step.Status);
        Assert.AreEqual("4E544C4D53535000030000001800", Convert.ToHexString(step.Token, 0, 14));
        Assert.AreEqual(0xA2888205u, BitConverter.ToUInt32(step.Token, 60));
        int userLength = BitConverter.ToUInt16(step.Token, 36);
        int userOffset = BitConverter.ToInt32(step.Token, 40);
        Assert.AreEqual("u", System.Text.Encoding.Unicode.GetString(step.Token, userOffset, userLength));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task NextTokenAsync_WindowsNtlmChallengeSspiCannotRead_AnswersMalformedToken()
    {
        using ISecurityContext context = new SystemSecurityContextFactory().Create(Ntlm());
        await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        SecurityContextStep step = await context.NextTokenAsync(Convert.FromBase64String("TlRMTVNTUAACAAAA"), CancellationToken.None);

        Assert.AreEqual(SecurityContextStatus.MalformedToken, step.Status);
        Assert.IsEmpty(step.Token);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task NextTokenAsync_WindowsKerberosExplicitCredentialOffDomain_AnswersNoCredentials()
    {
        using ISecurityContext context = new SystemSecurityContextFactory().Create(new SecurityContextRequest(SecurityMechanism.Kerberos, "HTTP", "127.0.0.1") { UserName = "u", Password = "p" });

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.AreEqual(SecurityContextStatus.NoCredentials, step.Status);
        Assert.IsEmpty(step.Token);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public async Task NextTokenAsync_LinuxNtlmWithoutGssNtlmssp_AnswersNoMechanism()
    {
        using ISecurityContext context = new SystemSecurityContextFactory().Create(Ntlm());

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.AreEqual(SecurityContextStatus.NoMechanism, step.Status);
        Assert.IsEmpty(step.Token);
    }

    [TestMethod]
    public void Dispose_BeforeAnyStep_DoesNotThrow()
    {
        ISecurityContext context = new SystemSecurityContextFactory().Create(Ntlm());

        context.Dispose();

        Assert.IsFalse(context.IsCompleted);
    }

    [TestMethod]
    public async Task NextTokenAsync_Cancelled_Throws()
    {
        using ISecurityContext context = new SystemSecurityContextFactory().Create(Ntlm());

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, new CancellationToken(canceled: true)).AsTask());
    }

    /// <summary>
    /// Establishes NTLM with the logged-on user's credential against an in-process SSPI
    /// acceptor, then wraps and unwraps each way; SSPI's own keys are the only ones that can
    /// check the other side, so the acceptor is the BCL's server-side
    /// <see cref="NegotiateAuthentication" />.
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(true)]
    [DataRow(false)]
    public async Task WrapAndUnwrap_WindowsNtlmCompleted_AreSspisMessageProtection(bool encrypt)
    {
        byte[] message = [0x01, 0x00, 0x10, 0x00];
        using ISecurityContext context = new SystemSecurityContextFactory().Create(
            new SecurityContextRequest(SecurityMechanism.Ntlm, "HTTP", "localhost") { MessageProtection = ProtectionLevel.EncryptAndSign });
        using NegotiateAuthentication acceptor = new(new NegotiateAuthenticationServerOptions { Package = "NTLM" });
        await EstablishAsync(context, acceptor);

        ArrayBufferWriter<byte> received = new();
        NegotiateAuthenticationStatusCode unwrapCode = acceptor.Unwrap(context.Wrap(message, encrypt)!, received, out _);
        ArrayBufferWriter<byte> reply = new();
        acceptor.Wrap(message, reply, encrypt, out _);
        byte[]? unwrapped = context.Unwrap(reply.WrittenSpan);

        Assert.AreEqual(NegotiateAuthenticationStatusCode.Completed, unwrapCode);
        CollectionAssert.AreEqual(message, received.WrittenSpan.ToArray());
        CollectionAssert.AreEqual(message, unwrapped);
        Assert.IsNull(context.Unwrap([0x00, 0x01, 0x02]), "A message SSPI cannot check does not unwrap.");
    }

    [TestMethod]
    public void WrapAndUnwrap_BeforeTheContextCompletes_Throw()
    {
        using ISecurityContext context = new SystemSecurityContextFactory().Create(Ntlm());

        Assert.ThrowsExactly<InvalidOperationException>(() => context.Wrap([0x01], encrypt: true));
        Assert.ThrowsExactly<InvalidOperationException>(() => context.Unwrap([0x01]));
    }

    [TestMethod]
    public void Create_NullRequest_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SystemSecurityContextFactory().Create(null!));
    }

    [TestMethod]
    [DataRow(NegotiateAuthenticationStatusCode.Completed, SecurityContextStatus.Completed)]
    [DataRow(NegotiateAuthenticationStatusCode.ContinueNeeded, SecurityContextStatus.ContinueNeeded)]
    [DataRow(NegotiateAuthenticationStatusCode.Unsupported, SecurityContextStatus.NoMechanism)]
    [DataRow(NegotiateAuthenticationStatusCode.UnknownCredentials, SecurityContextStatus.NoCredentials)]
    [DataRow(NegotiateAuthenticationStatusCode.CredentialsExpired, SecurityContextStatus.NoCredentials)]
    [DataRow(NegotiateAuthenticationStatusCode.InvalidToken, SecurityContextStatus.MalformedToken)]
    [DataRow(NegotiateAuthenticationStatusCode.MessageAltered, SecurityContextStatus.MalformedToken)]
    [DataRow(NegotiateAuthenticationStatusCode.GenericFailure, SecurityContextStatus.Refused)]
    [DataRow(NegotiateAuthenticationStatusCode.TargetUnknown, SecurityContextStatus.Refused)]
    public void StatusOf_EachCode_MapsAsAdr0142Routes(NegotiateAuthenticationStatusCode code, SecurityContextStatus expected)
    {
        Assert.AreEqual(expected, NegotiateAuthenticationStatusMapping.StatusOf(code));
    }

    /// <summary>Steps <paramref name="context" /> against <paramref name="acceptor" /> until both are established.</summary>
    private static async Task EstablishAsync(ISecurityContext context, NegotiateAuthentication acceptor)
    {
        byte[] incoming = [];
        while (!context.IsCompleted)
        {
            SecurityContextStep step = await context.NextTokenAsync(incoming, CancellationToken.None);
            Assert.IsTrue(step.Status is SecurityContextStatus.ContinueNeeded or SecurityContextStatus.Completed, $"The initiator's step came to {step.Status}.");
            incoming = acceptor.GetOutgoingBlob(step.Token, out NegotiateAuthenticationStatusCode code) ?? [];
            Assert.IsTrue(code is NegotiateAuthenticationStatusCode.ContinueNeeded or NegotiateAuthenticationStatusCode.Completed, $"The acceptor's step came to {code}.");
        }

        Assert.IsTrue(acceptor.IsAuthenticated);
    }

    private static SecurityContextRequest Ntlm() => new(SecurityMechanism.Ntlm, "HTTP", "127.0.0.1") { UserName = "u", Password = "p" };
}
