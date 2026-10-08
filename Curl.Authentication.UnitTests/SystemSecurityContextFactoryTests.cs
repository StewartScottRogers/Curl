using System.Buffers;
using System.Net;
using System.Net.Security;
using System.Security.Principal;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(SecurityMechanism.Ntlm, "NTLM")]
    [DataRow(SecurityMechanism.Negotiate, "Negotiate")]
    [DataRow(SecurityMechanism.Kerberos, "Kerberos")]
    public void OptionsFor_Mechanism_NamesItsPackageAndTheServiceSlashHostTarget(SecurityMechanism mechanism, string package)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", mechanism);
        diagnostics.Arrange("expected package", package);
        diagnostics.Arrange("service", "HTTP");
        diagnostics.Arrange("host", "server.example.test");

        NegotiateAuthenticationClientOptions options = SystemSecurityContextFactory.OptionsFor(new SecurityContextRequest(mechanism, "HTTP", "server.example.test"));

        diagnostics.Act("package", options.Package);
        diagnostics.Act("target name", options.TargetName);
        diagnostics.Assert("package", package, options.Package);
        diagnostics.Assert("target name", "HTTP/server.example.test", options.TargetName);
        diagnostics.Assert("impersonation level", TokenImpersonationLevel.None, options.AllowedImpersonationLevel);
        diagnostics.Assert("protection level", ProtectionLevel.None, options.RequiredProtectionLevel);
        Assert.AreEqual(package, options.Package);
        Assert.AreEqual("HTTP/server.example.test", options.TargetName);
        Assert.AreSame(CredentialCache.DefaultNetworkCredentials, options.Credential);
        Assert.AreEqual(TokenImpersonationLevel.None, options.AllowedImpersonationLevel);
        Assert.AreEqual(ProtectionLevel.None, options.RequiredProtectionLevel);
    }

    [TestMethod]
    public void OptionsFor_ExplicitCredentialAndAlwaysDelegation_PassesBoth()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        SecurityContextRequest request = new(SecurityMechanism.Negotiate, "HTTP", "h") { UserName = "alice", Password = "pw", Domain = "EXAMPLE", Delegation = SecurityDelegation.Always };
        diagnostics.Arrange("user name", "alice");
        diagnostics.Arrange("password", "pw");
        diagnostics.Arrange("domain", "EXAMPLE");
        diagnostics.Arrange("delegation", SecurityDelegation.Always);

        NegotiateAuthenticationClientOptions options = SystemSecurityContextFactory.OptionsFor(request);

        diagnostics.Act("credential user name", options.Credential.UserName);
        diagnostics.Act("impersonation level", options.AllowedImpersonationLevel);
        diagnostics.Assert("credential user name", "alice", options.Credential.UserName);
        diagnostics.Assert("credential password", "pw", options.Credential.Password);
        diagnostics.Assert("credential domain", "EXAMPLE", options.Credential.Domain);
        diagnostics.Assert("impersonation level", TokenImpersonationLevel.Delegation, options.AllowedImpersonationLevel);
        Assert.AreEqual("alice", options.Credential.UserName);
        Assert.AreEqual("pw", options.Credential.Password);
        Assert.AreEqual("EXAMPLE", options.Credential.Domain);
        Assert.AreEqual(TokenImpersonationLevel.Delegation, options.AllowedImpersonationLevel);
    }

    [TestMethod]
    public void OptionsFor_PolicyDelegation_AsksNoneForWantOfAPolicyFlag()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        SecurityContextRequest request = new(SecurityMechanism.Negotiate, "HTTP", "h") { Delegation = SecurityDelegation.Policy };
        diagnostics.Arrange("delegation", SecurityDelegation.Policy);

        TokenImpersonationLevel level = SystemSecurityContextFactory.OptionsFor(request).AllowedImpersonationLevel;

        diagnostics.Act("impersonation level", level);
        diagnostics.Assert("impersonation level", TokenImpersonationLevel.None, level);
        Assert.AreEqual(TokenImpersonationLevel.None, SystemSecurityContextFactory.OptionsFor(request).AllowedImpersonationLevel);
    }

    [TestMethod]
    public void OptionsFor_OtherServiceName_NamesItsServiceSlashHostTarget()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("service", "svc");
        diagnostics.Arrange("host", "proxy.example.test");

        NegotiateAuthenticationClientOptions options = SystemSecurityContextFactory.OptionsFor(new SecurityContextRequest(SecurityMechanism.Negotiate, "svc", "proxy.example.test"));

        diagnostics.Act("target name", options.TargetName);
        diagnostics.Assert("target name", "svc/proxy.example.test", options.TargetName);
        Assert.AreEqual("svc/proxy.example.test", options.TargetName);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task NextTokenAsync_WindowsNtlmExplicitCredential_GivesSspisType1()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user name", "u");
        diagnostics.Arrange("password", "p");
        using ISecurityContext context = new SystemSecurityContextFactory().Create(Ntlm());

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        diagnostics.Act("status", step.Status);
        diagnostics.Bytes("token", step.Token);
        diagnostics.Assert("status", SecurityContextStatus.ContinueNeeded, step.Status);
        diagnostics.Diff("token start hex", MeasuredType1Start, Convert.ToHexString(step.Token, 0, 16));
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
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user name", "u");
        diagnostics.Arrange("password", "p");
        diagnostics.Bytes("challenge", Convert.FromBase64String(HandBuiltNtlmSecurityContextTests.MeasuredChallenge));
        using ISecurityContext context = new SystemSecurityContextFactory().Create(Ntlm());
        await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        SecurityContextStep step = await context.NextTokenAsync(Convert.FromBase64String(HandBuiltNtlmSecurityContextTests.MeasuredChallenge), CancellationToken.None);

        diagnostics.Act("status", step.Status);
        diagnostics.Bytes("token", step.Token);
        diagnostics.Assert("status", SecurityContextStatus.Completed, step.Status);
        diagnostics.Diff("token start hex", "4E544C4D53535000030000001800", Convert.ToHexString(step.Token, 0, 14));
        diagnostics.Assert("flags", 0xA2888205u, BitConverter.ToUInt32(step.Token, 60));
        Assert.AreEqual(SecurityContextStatus.Completed, step.Status);
        Assert.AreEqual("4E544C4D53535000030000001800", Convert.ToHexString(step.Token, 0, 14));
        Assert.AreEqual(0xA2888205u, BitConverter.ToUInt32(step.Token, 60));
        int userLength = BitConverter.ToUInt16(step.Token, 36);
        int userOffset = BitConverter.ToInt32(step.Token, 40);
        diagnostics.Diff("user name", "u", System.Text.Encoding.Unicode.GetString(step.Token, userOffset, userLength));
        Assert.AreEqual("u", System.Text.Encoding.Unicode.GetString(step.Token, userOffset, userLength));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task NextTokenAsync_WindowsNtlmChallengeSspiCannotRead_AnswersMalformedToken()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user name", "u");
        diagnostics.Arrange("password", "p");
        diagnostics.Bytes("challenge", Convert.FromBase64String("TlRMTVNTUAACAAAA"));
        using ISecurityContext context = new SystemSecurityContextFactory().Create(Ntlm());
        await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        SecurityContextStep step = await context.NextTokenAsync(Convert.FromBase64String("TlRMTVNTUAACAAAA"), CancellationToken.None);

        diagnostics.Act("status", step.Status);
        diagnostics.Bytes("token", step.Token);
        diagnostics.Assert("status", SecurityContextStatus.MalformedToken, step.Status);
        diagnostics.Assert("token length", 0, step.Token.Length);
        Assert.AreEqual(SecurityContextStatus.MalformedToken, step.Status);
        Assert.IsEmpty(step.Token);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task NextTokenAsync_WindowsKerberosExplicitCredentialOffDomain_AnswersNoCredentials()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", SecurityMechanism.Kerberos);
        diagnostics.Arrange("host", "127.0.0.1");
        diagnostics.Arrange("user name", "u");
        diagnostics.Arrange("password", "p");
        using ISecurityContext context = new SystemSecurityContextFactory().Create(new SecurityContextRequest(SecurityMechanism.Kerberos, "HTTP", "127.0.0.1") { UserName = "u", Password = "p" });

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        diagnostics.Act("status", step.Status);
        diagnostics.Bytes("token", step.Token);
        diagnostics.Assert("status", SecurityContextStatus.NoCredentials, step.Status);
        diagnostics.Assert("token length", 0, step.Token.Length);
        Assert.AreEqual(SecurityContextStatus.NoCredentials, step.Status);
        Assert.IsEmpty(step.Token);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public async Task NextTokenAsync_LinuxNtlmWithoutGssNtlmssp_AnswersNoMechanism()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user name", "u");
        diagnostics.Arrange("password", "p");
        using ISecurityContext context = new SystemSecurityContextFactory().Create(Ntlm());

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        diagnostics.Act("status", step.Status);
        diagnostics.Bytes("token", step.Token);
        diagnostics.Assert("status", SecurityContextStatus.NoMechanism, step.Status);
        diagnostics.Assert("token length", 0, step.Token.Length);
        Assert.AreEqual(SecurityContextStatus.NoMechanism, step.Status);
        Assert.IsEmpty(step.Token);
    }

    [TestMethod]
    public void Dispose_BeforeAnyStep_DoesNotThrow()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user name", "u");
        diagnostics.Arrange("password", "p");
        ISecurityContext context = new SystemSecurityContextFactory().Create(Ntlm());

        context.Dispose();

        diagnostics.Act("is completed after dispose", context.IsCompleted);
        diagnostics.Assert("is completed", false, context.IsCompleted);
        Assert.IsFalse(context.IsCompleted);
    }

    [TestMethod]
    public async Task NextTokenAsync_Cancelled_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user name", "u");
        diagnostics.Arrange("password", "p");
        diagnostics.Arrange("token cancelled", true);
        using ISecurityContext context = new SystemSecurityContextFactory().Create(Ntlm());

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, new CancellationToken(canceled: true)).AsTask());

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception type", typeof(OperationCanceledException), exception.GetType());
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
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] message = [0x01, 0x00, 0x10, 0x00];
        diagnostics.Arrange("encrypt", encrypt);
        diagnostics.Bytes("message", message);
        using ISecurityContext context = new SystemSecurityContextFactory().Create(
            new SecurityContextRequest(SecurityMechanism.Ntlm, "HTTP", "localhost") { MessageProtection = ProtectionLevel.EncryptAndSign });
        using NegotiateAuthentication acceptor = new(new NegotiateAuthenticationServerOptions { Package = "NTLM" });
        await EstablishAsync(context, acceptor);

        ArrayBufferWriter<byte> received = new();
        NegotiateAuthenticationStatusCode unwrapCode = acceptor.Unwrap(context.Wrap(message, encrypt)!, received, out _);
        ArrayBufferWriter<byte> reply = new();
        acceptor.Wrap(message, reply, encrypt, out _);
        byte[]? unwrapped = context.Unwrap(reply.WrittenSpan);

        diagnostics.Act("acceptor unwrap code", unwrapCode);
        diagnostics.Bytes("acceptor received", received.WrittenSpan);
        diagnostics.Bytes("initiator unwrapped", unwrapped ?? []);
        diagnostics.Assert("acceptor unwrap code", NegotiateAuthenticationStatusCode.Completed, unwrapCode);
        diagnostics.Diff("acceptor received", message, received.WrittenSpan);
        diagnostics.Diff("initiator unwrapped", message, unwrapped ?? []);
        Assert.AreEqual(NegotiateAuthenticationStatusCode.Completed, unwrapCode);
        CollectionAssert.AreEqual(message, received.WrittenSpan.ToArray());
        CollectionAssert.AreEqual(message, unwrapped);
        Assert.IsNull(context.Unwrap([0x00, 0x01, 0x02]), "A message SSPI cannot check does not unwrap.");
    }

    [TestMethod]
    public void WrapAndUnwrap_BeforeTheContextCompletes_Throw()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user name", "u");
        diagnostics.Arrange("password", "p");
        diagnostics.Bytes("message", new byte[] { 0x01 });
        using ISecurityContext context = new SystemSecurityContextFactory().Create(Ntlm());

        InvalidOperationException wrapException = Assert.ThrowsExactly<InvalidOperationException>(() => context.Wrap([0x01], encrypt: true));
        InvalidOperationException unwrapException = Assert.ThrowsExactly<InvalidOperationException>(() => context.Unwrap([0x01]));

        diagnostics.Act("wrap exception", wrapException.GetType().Name);
        diagnostics.Act("unwrap exception", unwrapException.GetType().Name);
        diagnostics.Assert("wrap exception type", typeof(InvalidOperationException), wrapException.GetType());
        diagnostics.Assert("unwrap exception type", typeof(InvalidOperationException), unwrapException.GetType());
    }

    [TestMethod]
    public void Create_NullRequest_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("request", null);

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => new SystemSecurityContextFactory().Create(null!));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception type", typeof(ArgumentNullException), exception.GetType());
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
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("status code", code);
        diagnostics.Arrange("expected status", expected);

        SecurityContextStatus status = NegotiateAuthenticationStatusMapping.StatusOf(code);

        diagnostics.Act("status", status);
        diagnostics.Assert("status", expected, status);
        Assert.AreEqual(expected, NegotiateAuthenticationStatusMapping.StatusOf(code));
    }

    [TestMethod]
    [DataRow(NegotiateAuthenticationStatusCode.ContinueNeeded, SecurityContextStatus.ContinueNeeded)]
    [DataRow(NegotiateAuthenticationStatusCode.Completed, SecurityContextStatus.Completed)]
    public void StepOf_GoingOnOrCompletedWithAToken_CarriesTheToken(NegotiateAuthenticationStatusCode code, SecurityContextStatus expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("status code", code);
        diagnostics.Arrange("expected status", expected);
        diagnostics.Bytes("outgoing token", new byte[] { 0x4E, 0x54 });

        SecurityContextStep step = SystemSecurityContext.StepOf(code, [0x4E, 0x54]);

        diagnostics.Act("status", step.Status);
        diagnostics.Bytes("token", step.Token);
        diagnostics.Assert("status", expected, step.Status);
        diagnostics.Diff("token", new byte[] { 0x4E, 0x54 }, step.Token);
        Assert.AreEqual(expected, step.Status);
        CollectionAssert.AreEqual(new byte[] { 0x4E, 0x54 }, step.Token);
    }

    [TestMethod]
    [DataRow(NegotiateAuthenticationStatusCode.ContinueNeeded, SecurityContextStatus.ContinueNeeded)]
    [DataRow(NegotiateAuthenticationStatusCode.Completed, SecurityContextStatus.Completed)]
    public void StepOf_GoingOnOrCompletedWithNoToken_CarriesAnEmptyToken(NegotiateAuthenticationStatusCode code, SecurityContextStatus expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("status code", code);
        diagnostics.Arrange("expected status", expected);
        diagnostics.Arrange("outgoing token", null);

        SecurityContextStep step = SystemSecurityContext.StepOf(code, null);

        diagnostics.Act("status", step.Status);
        diagnostics.Bytes("token", step.Token);
        diagnostics.Assert("status", expected, step.Status);
        diagnostics.Assert("token length", 0, step.Token.Length);
        Assert.AreEqual(expected, step.Status);
        Assert.IsEmpty(step.Token);
    }

    [TestMethod]
    public void StepOf_FailureWithAToken_DropsTheToken()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("status code", NegotiateAuthenticationStatusCode.InvalidToken);
        diagnostics.Bytes("outgoing token", new byte[] { 0x4E, 0x54 });

        SecurityContextStep step = SystemSecurityContext.StepOf(NegotiateAuthenticationStatusCode.InvalidToken, [0x4E, 0x54]);

        diagnostics.Act("status", step.Status);
        diagnostics.Bytes("token", step.Token);
        diagnostics.Assert("status", SecurityContextStatus.MalformedToken, step.Status);
        diagnostics.Assert("token length", 0, step.Token.Length);
        Assert.AreEqual(SecurityContextStatus.MalformedToken, step.Status);
        Assert.IsEmpty(step.Token);
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
