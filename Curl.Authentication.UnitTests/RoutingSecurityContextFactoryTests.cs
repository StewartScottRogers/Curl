using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="RoutingSecurityContextFactory" /> to ADR-0142's tables: SSPI for everything
/// on Windows; elsewhere hand-built NTLM, and the system GSS-API for Negotiate and Kerberos
/// with the default credentials, falling back to the hand-built route only on
/// <see cref="SecurityContextStatus.NoMechanism" />.
/// </summary>
[TestClass]
public sealed class RoutingSecurityContextFactoryTests
{
    private static readonly SecurityContextRequest Explicit =
        new(SecurityMechanism.Negotiate, "HTTP", "server.example.test") { UserName = "alice", Password = "pw", Domain = "EXAMPLE" };

    private static readonly SecurityContextStep Token = new(SecurityContextStatus.ContinueNeeded, [0x01]);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Create_WindowsNegotiateFallingBackToNtlm_AnswersNoCredentialsAsCurlsSspiDoes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("request", Explicit);
        ScriptedSecurityContext sspi = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [.. "NTLMSSP\0"u8, 0x01]));
        using ISecurityContext context = new RoutingSecurityContextFactory(isWindows: true, new ScriptedSecurityContextFactory(sspi), new ScriptedSecurityContextFactory()).Create(Explicit);

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        diagnostics.Act("status", step.Status);
        diagnostics.Bytes("token", step.Token);
        diagnostics.Assert("status", SecurityContextStatus.NoCredentials, step.Status);
        Assert.AreEqual(SecurityContextStatus.NoCredentials, step.Status);
        Assert.IsEmpty(step.Token);
    }

    [TestMethod]
    public async Task Create_WindowsNegotiateWithKerberos_PassesEveryStepThrough()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] kerberos = SpnegoInitialToken.Encode(SpnegoMechanism.MitKerberosMechanismTypes, [0x60, 0x00]);
        diagnostics.Arrange("request", Explicit);
        diagnostics.Bytes("kerberos token", kerberos);
        SecurityContextStep ntlmLookingLaterStep = new(SecurityContextStatus.Completed, [.. "NTLMSSP\0"u8]);
        ScriptedSecurityContext sspi = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, kerberos), ntlmLookingLaterStep) { IsCompleted = true };
        ISecurityContext context = new RoutingSecurityContextFactory(isWindows: true, new ScriptedSecurityContextFactory(sspi), new ScriptedSecurityContextFactory()).Create(Explicit);

        SecurityContextStep first = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        SecurityContextStep second = await context.NextTokenAsync(new byte[] { 0x01 }, CancellationToken.None);
        context.Dispose();

        diagnostics.Act("first status", first.Status);
        diagnostics.Act("second status", second.Status);
        diagnostics.Assert("first token", kerberos.Length, first.Token.Length);
        diagnostics.Assert("second step", ntlmLookingLaterStep, second);
        Assert.AreSame(kerberos, first.Token);
        Assert.AreEqual(ntlmLookingLaterStep, second);
        Assert.IsTrue(context.IsCompleted);
        Assert.IsTrue(sspi.IsDisposed);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task Create_WindowsNegotiateWithoutADomain_MakesNoTokenAsCurl8210Measured()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", SecurityMechanism.Negotiate);
        diagnostics.Arrange("host", "127.0.0.1");
        using ISecurityContext context = ProductionRouter(isWindows: true).Create(new SecurityContextRequest(SecurityMechanism.Negotiate, "HTTP", "127.0.0.1"));

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        diagnostics.Act("status", step.Status);
        diagnostics.Assert("status", SecurityContextStatus.NoCredentials, step.Status);
        Assert.AreEqual(SecurityContextStatus.NoCredentials, step.Status);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task Create_NegotiateOffWindowsWithoutATicket_MakesNoTokenAsCurl8180Measured()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", SecurityMechanism.Negotiate);
        diagnostics.Arrange("host", "127.0.0.1");
        using ISecurityContext context = ProductionRouter(isWindows: false).Create(new SecurityContextRequest(SecurityMechanism.Negotiate, "HTTP", "127.0.0.1"));

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        diagnostics.Act("status", step.Status);
        diagnostics.Bytes("token", step.Token);
        diagnostics.Assert("token length", 0, step.Token.Length);
        Assert.IsEmpty(step.Token);
        Assert.AreNotEqual(SecurityContextStatus.ContinueNeeded, step.Status);
    }

    [TestMethod]
    public async Task Create_NtlmOffWindows_SendsCurlsOwnType1AsCurl8180Measured()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", SecurityMechanism.Ntlm);
        diagnostics.Arrange("credentials", "u:p");
        using ISecurityContext context = ProductionRouter(isWindows: false).Create(new SecurityContextRequest(SecurityMechanism.Ntlm, "HTTP", "127.0.0.1") { UserName = "u", Password = "p" });

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        diagnostics.Act("status", step.Status);
        diagnostics.Bytes("token", step.Token);
        diagnostics.Diff("type 1", HandBuiltNtlmSecurityContextTests.CurlType1, Convert.ToBase64String(step.Token));
        Assert.AreEqual(SecurityContextStatus.ContinueNeeded, step.Status);
        Assert.AreEqual(HandBuiltNtlmSecurityContextTests.CurlType1, Convert.ToBase64String(step.Token));
    }

    [TestMethod]
    [DataRow(SecurityMechanism.Ntlm)]
    [DataRow(SecurityMechanism.Kerberos)]
    public void Create_Windows_UsesSspiWithTheRequestAsGiven(SecurityMechanism mechanism)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", mechanism);
        ScriptedSecurityContext sspi = new();
        ScriptedSecurityContextFactory system = new(sspi);
        ScriptedSecurityContextFactory handBuilt = new();

        ISecurityContext context = new RoutingSecurityContextFactory(isWindows: true, system, handBuilt).Create(Explicit with { Mechanism = mechanism });

        diagnostics.Act("system requests", system.Requests.Count);
        diagnostics.Act("hand-built requests", handBuilt.Requests.Count);
        diagnostics.Assert("request", Explicit with { Mechanism = mechanism }, system.Requests.Single());
        Assert.AreSame(sspi, context);
        Assert.AreEqual(Explicit with { Mechanism = mechanism }, system.Requests.Single());
        Assert.IsEmpty(handBuilt.Requests);
    }

    [TestMethod]
    [DataRow(SecurityMechanism.Negotiate)]
    [DataRow(SecurityMechanism.Kerberos)]
    public void Create_WindowsWithDelegation_AsksSspiForNoneAsCurlsSspiCodeDoes(SecurityMechanism mechanism)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", mechanism);
        diagnostics.Arrange("delegation", SecurityDelegation.Always);
        ScriptedSecurityContextFactory system = new(new ScriptedSecurityContext());

        using ISecurityContext context = new RoutingSecurityContextFactory(isWindows: true, system, new ScriptedSecurityContextFactory()).Create(Explicit with { Mechanism = mechanism, Delegation = SecurityDelegation.Always });

        diagnostics.Act("system request", system.Requests.Single());
        diagnostics.Assert("request", Explicit with { Mechanism = mechanism }, system.Requests.Single());
        Assert.AreEqual(Explicit with { Mechanism = mechanism }, system.Requests.Single());
    }

    [TestMethod]
    public void Create_GssApiOffWindowsWithDelegation_PassesItOn()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("service", "svc");
        diagnostics.Arrange("delegation", SecurityDelegation.Always);
        ScriptedSecurityContextFactory system = new(new ScriptedSecurityContext());

        using ISecurityContext context = new RoutingSecurityContextFactory(isWindows: false, system, new ScriptedSecurityContextFactory()).Create(Explicit with { ServiceName = "svc", Delegation = SecurityDelegation.Always });

        SecurityContextRequest expected = new(SecurityMechanism.Negotiate, "svc", "server.example.test") { Delegation = SecurityDelegation.Always };
        diagnostics.Act("system request", system.Requests.Single());
        diagnostics.Assert("request", expected, system.Requests.Single());
        Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Negotiate, "svc", "server.example.test") { Delegation = SecurityDelegation.Always }, system.Requests.Single());
    }

    [TestMethod]
    public void Create_NtlmOffWindows_UsesTheHandBuiltRoute()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", SecurityMechanism.Ntlm);
        ScriptedSecurityContext ntlm = new();
        ScriptedSecurityContextFactory system = new();
        ScriptedSecurityContextFactory handBuilt = new(ntlm);

        ISecurityContext context = new RoutingSecurityContextFactory(isWindows: false, system, handBuilt).Create(Explicit with { Mechanism = SecurityMechanism.Ntlm });

        diagnostics.Act("hand-built user", handBuilt.Requests.Single().UserName);
        diagnostics.Act("system requests", system.Requests.Count);
        diagnostics.Assert("user", "alice", handBuilt.Requests.Single().UserName);
        Assert.AreSame(ntlm, context);
        Assert.AreEqual("alice", handBuilt.Requests.Single().UserName);
        Assert.IsEmpty(system.Requests);
    }

    [TestMethod]
    [DataRow(SecurityMechanism.Negotiate)]
    [DataRow(SecurityMechanism.Kerberos)]
    public async Task Create_GssApiAnswersOffWindows_UsesItWithTheDefaultCredentials(SecurityMechanism mechanism)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", mechanism);
        ScriptedSecurityContext gss = new(Token, new SecurityContextStep(SecurityContextStatus.Completed, [])) { IsCompleted = true };
        ScriptedSecurityContextFactory system = new(gss);
        ScriptedSecurityContextFactory handBuilt = new();
        using ISecurityContext context = new RoutingSecurityContextFactory(isWindows: false, system, handBuilt).Create(Explicit with { Mechanism = mechanism });

        SecurityContextStep first = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        SecurityContextStep second = await context.NextTokenAsync(new byte[] { 0x02 }, CancellationToken.None);

        diagnostics.Act("first", first);
        diagnostics.Act("second status", second.Status);
        diagnostics.Assert("first", Token, first);
        diagnostics.Assert("request", new SecurityContextRequest(mechanism, "HTTP", "server.example.test"), system.Requests.Single());
        Assert.AreEqual(Token, first);
        Assert.AreEqual(SecurityContextStatus.Completed, second.Status);
        Assert.IsTrue(context.IsCompleted);
        Assert.AreEqual(new SecurityContextRequest(mechanism, "HTTP", "server.example.test"), system.Requests.Single());
        Assert.IsEmpty(handBuilt.Requests);
    }

    [TestMethod]
    public async Task Create_GssApiUnsupportedOffWindows_FallsBackToTheHandBuiltRoute()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("gss status", SecurityContextStatus.NoMechanism);
        ScriptedSecurityContext gss = new(new SecurityContextStep(SecurityContextStatus.NoMechanism, []));
        ScriptedSecurityContext spnego = new(Token, new SecurityContextStep(SecurityContextStatus.Completed, []));
        ScriptedSecurityContextFactory handBuilt = new(spnego);
        using ISecurityContext context = new RoutingSecurityContextFactory(isWindows: false, new ScriptedSecurityContextFactory(gss), handBuilt).Create(Explicit);

        SecurityContextStep first = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        SecurityContextStep second = await context.NextTokenAsync(new byte[] { 0x02 }, CancellationToken.None);

        diagnostics.Act("first", first);
        diagnostics.Act("second status", second.Status);
        diagnostics.Assert("first", Token, first);
        diagnostics.Assert("incoming tokens", 2, spnego.IncomingTokens.Count);
        Assert.AreEqual(Token, first);
        Assert.AreEqual(SecurityContextStatus.Completed, second.Status);
        Assert.IsTrue(gss.IsDisposed);
        Assert.AreEqual(2, spnego.IncomingTokens.Count);
        Assert.IsNull(handBuilt.Requests.Single().UserName);
        context.Dispose();
        Assert.IsTrue(spnego.IsDisposed);
    }

    [TestMethod]
    public async Task Create_GssApiHasNoTicketOffWindows_DoesNotFallBack()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("gss status", SecurityContextStatus.NoCredentials);
        ScriptedSecurityContext gss = new(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));
        ScriptedSecurityContextFactory handBuilt = new();
        using ISecurityContext context = new RoutingSecurityContextFactory(isWindows: false, new ScriptedSecurityContextFactory(gss), handBuilt).Create(Explicit);

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        diagnostics.Act("status", step.Status);
        diagnostics.Act("hand-built requests", handBuilt.Requests.Count);
        diagnostics.Assert("status", SecurityContextStatus.NoCredentials, step.Status);
        Assert.AreEqual(SecurityContextStatus.NoCredentials, step.Status);
        Assert.IsEmpty(handBuilt.Requests);
    }

    [TestMethod]
    public async Task Create_NoMechanismOnALaterStep_DoesNotFallBack()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("gss second status", SecurityContextStatus.NoMechanism);
        ScriptedSecurityContext gss = new(Token, new SecurityContextStep(SecurityContextStatus.NoMechanism, []));
        ScriptedSecurityContextFactory handBuilt = new();
        using ISecurityContext context = new RoutingSecurityContextFactory(isWindows: false, new ScriptedSecurityContextFactory(gss), handBuilt).Create(Explicit);

        await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        SecurityContextStep second = await context.NextTokenAsync(new byte[] { 0x02 }, CancellationToken.None);

        diagnostics.Act("second status", second.Status);
        diagnostics.Act("hand-built requests", handBuilt.Requests.Count);
        diagnostics.Assert("second status", SecurityContextStatus.NoMechanism, second.Status);
        Assert.AreEqual(SecurityContextStatus.NoMechanism, second.Status);
        Assert.IsEmpty(handBuilt.Requests);
    }

    [TestMethod]
    public void Create_WindowsNegotiate_WrapsAndUnwrapsThroughSspi()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Bytes("to wrap", [0x01]);
        diagnostics.Bytes("to unwrap", [(byte)'S', 0x02]);
        ScriptedSecurityContext sspi = new() { IsCompleted = true };
        using ISecurityContext context = new RoutingSecurityContextFactory(isWindows: true, new ScriptedSecurityContextFactory(sspi), new ScriptedSecurityContextFactory()).Create(Explicit);
        diagnostics.Arrange("request", Explicit);

        byte[]? wrapped = context.Wrap([0x01], encrypt: true);
        byte[]? unwrapped = context.Unwrap([(byte)'S', 0x02]);

        diagnostics.Bytes("wrapped", wrapped);
        diagnostics.Bytes("unwrapped", unwrapped);
        diagnostics.Act("wrapped length", wrapped?.Length ?? -1);
        diagnostics.Diff("wrapped", new byte[] { (byte)'E', 0x01 }, wrapped);
        diagnostics.Diff("unwrapped", new byte[] { 0x02 }, unwrapped);
        CollectionAssert.AreEqual(new byte[] { (byte)'E', 0x01 }, wrapped);
        CollectionAssert.AreEqual(new byte[] { 0x02 }, unwrapped);
    }

    [TestMethod]
    public async Task Create_GssApiUnsupportedOffWindows_WrapsAndUnwrapsOnTheHandBuiltRoute()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("gss status", SecurityContextStatus.NoMechanism);
        ScriptedSecurityContext gss = new(new SecurityContextStep(SecurityContextStatus.NoMechanism, []));
        ScriptedSecurityContext spnego = new(new SecurityContextStep(SecurityContextStatus.Completed, [])) { IsCompleted = true };
        using ISecurityContext context = new RoutingSecurityContextFactory(isWindows: false, new ScriptedSecurityContextFactory(gss), new ScriptedSecurityContextFactory(spnego)).Create(Explicit);
        await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        byte[]? wrapped = context.Wrap([0x01], encrypt: false);
        byte[]? unwrapped = context.Unwrap([]);

        diagnostics.Bytes("wrapped", wrapped);
        diagnostics.Act("unwrapped", unwrapped is null ? "null" : "not null");
        diagnostics.Diff("wrapped", new byte[] { (byte)'S', 0x01 }, wrapped);
        diagnostics.Assert("unwrapped", null, unwrapped);
        CollectionAssert.AreEqual(new byte[] { (byte)'S', 0x01 }, wrapped);
        Assert.IsNull(unwrapped);
    }

    /// <summary>The router as composed in production, with a hand-built route that finds no credential cache.</summary>
    private static RoutingSecurityContextFactory ProductionRouter(bool isWindows) => new(
        isWindows,
        new SystemSecurityContextFactory(),
        new HandBuiltSecurityContextFactory(
            new KerberosServiceTicketSource(
                () => Kerberos.KerberosConfiguration.Empty,
                () => throw new Kerberos.KerberosFileException(Kerberos.KerberosFileError.NotFound),
                _ => throw new AssertFailedException("No KDC client is needed.")),
            TimeProvider.System,
            new Kerberos.SystemKerberosRandomSource(),
            new Ntlm.SystemNtlmRandomSource()));

    [TestMethod]
    public void Create_NullRequest_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("request", "null");
        RoutingSecurityContextFactory factory = new(isWindows: false, new ScriptedSecurityContextFactory(), new ScriptedSecurityContextFactory());

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => factory.Create(null!));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("parameter", "request", exception.ParamName);
    }
}
