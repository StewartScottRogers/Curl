using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public async Task Create_WindowsNegotiateFallingBackToNtlm_AnswersNoCredentialsAsCurlsSspiDoes()
    {
        ScriptedSecurityContext sspi = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [.. "NTLMSSP\0"u8, 0x01]));
        using ISecurityContext context = new RoutingSecurityContextFactory(isWindows: true, new ScriptedSecurityContextFactory(sspi), new ScriptedSecurityContextFactory()).Create(Explicit);

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.AreEqual(SecurityContextStatus.NoCredentials, step.Status);
        Assert.IsEmpty(step.Token);
    }

    [TestMethod]
    public async Task Create_WindowsNegotiateWithKerberos_PassesEveryStepThrough()
    {
        byte[] kerberos = SpnegoInitialToken.Encode(SpnegoMechanism.MitKerberosMechanismTypes, [0x60, 0x00]);
        SecurityContextStep ntlmLookingLaterStep = new(SecurityContextStatus.Completed, [.. "NTLMSSP\0"u8]);
        ScriptedSecurityContext sspi = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, kerberos), ntlmLookingLaterStep) { IsCompleted = true };
        ISecurityContext context = new RoutingSecurityContextFactory(isWindows: true, new ScriptedSecurityContextFactory(sspi), new ScriptedSecurityContextFactory()).Create(Explicit);

        SecurityContextStep first = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        SecurityContextStep second = await context.NextTokenAsync(new byte[] { 0x01 }, CancellationToken.None);
        context.Dispose();

        Assert.AreSame(kerberos, first.Token);
        Assert.AreEqual(ntlmLookingLaterStep, second);
        Assert.IsTrue(context.IsCompleted);
        Assert.IsTrue(sspi.IsDisposed);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task Create_WindowsNegotiateWithoutADomain_MakesNoTokenAsCurl8210Measured()
    {
        using ISecurityContext context = ProductionRouter(isWindows: true).Create(new SecurityContextRequest(SecurityMechanism.Negotiate, "HTTP", "127.0.0.1"));

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.AreEqual(SecurityContextStatus.NoCredentials, step.Status);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task Create_NegotiateOffWindowsWithoutATicket_MakesNoTokenAsCurl8180Measured()
    {
        using ISecurityContext context = ProductionRouter(isWindows: false).Create(new SecurityContextRequest(SecurityMechanism.Negotiate, "HTTP", "127.0.0.1"));

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.IsEmpty(step.Token);
        Assert.AreNotEqual(SecurityContextStatus.ContinueNeeded, step.Status);
    }

    [TestMethod]
    public async Task Create_NtlmOffWindows_SendsCurlsOwnType1AsCurl8180Measured()
    {
        using ISecurityContext context = ProductionRouter(isWindows: false).Create(new SecurityContextRequest(SecurityMechanism.Ntlm, "HTTP", "127.0.0.1") { UserName = "u", Password = "p" });

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.AreEqual(SecurityContextStatus.ContinueNeeded, step.Status);
        Assert.AreEqual(HandBuiltNtlmSecurityContextTests.CurlType1, Convert.ToBase64String(step.Token));
    }

    [TestMethod]
    [DataRow(SecurityMechanism.Ntlm)]
    [DataRow(SecurityMechanism.Kerberos)]
    public void Create_Windows_UsesSspiWithTheRequestAsGiven(SecurityMechanism mechanism)
    {
        ScriptedSecurityContext sspi = new();
        ScriptedSecurityContextFactory system = new(sspi);
        ScriptedSecurityContextFactory handBuilt = new();

        ISecurityContext context = new RoutingSecurityContextFactory(isWindows: true, system, handBuilt).Create(Explicit with { Mechanism = mechanism });

        Assert.AreSame(sspi, context);
        Assert.AreEqual(Explicit with { Mechanism = mechanism }, system.Requests.Single());
        Assert.IsEmpty(handBuilt.Requests);
    }

    [TestMethod]
    public void Create_NtlmOffWindows_UsesTheHandBuiltRoute()
    {
        ScriptedSecurityContext ntlm = new();
        ScriptedSecurityContextFactory system = new();
        ScriptedSecurityContextFactory handBuilt = new(ntlm);

        ISecurityContext context = new RoutingSecurityContextFactory(isWindows: false, system, handBuilt).Create(Explicit with { Mechanism = SecurityMechanism.Ntlm });

        Assert.AreSame(ntlm, context);
        Assert.AreEqual("alice", handBuilt.Requests.Single().UserName);
        Assert.IsEmpty(system.Requests);
    }

    [TestMethod]
    [DataRow(SecurityMechanism.Negotiate)]
    [DataRow(SecurityMechanism.Kerberos)]
    public async Task Create_GssApiAnswersOffWindows_UsesItWithTheDefaultCredentials(SecurityMechanism mechanism)
    {
        ScriptedSecurityContext gss = new(Token, new SecurityContextStep(SecurityContextStatus.Completed, [])) { IsCompleted = true };
        ScriptedSecurityContextFactory system = new(gss);
        ScriptedSecurityContextFactory handBuilt = new();
        using ISecurityContext context = new RoutingSecurityContextFactory(isWindows: false, system, handBuilt).Create(Explicit with { Mechanism = mechanism });

        SecurityContextStep first = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        SecurityContextStep second = await context.NextTokenAsync(new byte[] { 0x02 }, CancellationToken.None);

        Assert.AreEqual(Token, first);
        Assert.AreEqual(SecurityContextStatus.Completed, second.Status);
        Assert.IsTrue(context.IsCompleted);
        Assert.AreEqual(new SecurityContextRequest(mechanism, "HTTP", "server.example.test"), system.Requests.Single());
        Assert.IsEmpty(handBuilt.Requests);
    }

    [TestMethod]
    public async Task Create_GssApiUnsupportedOffWindows_FallsBackToTheHandBuiltRoute()
    {
        ScriptedSecurityContext gss = new(new SecurityContextStep(SecurityContextStatus.NoMechanism, []));
        ScriptedSecurityContext spnego = new(Token, new SecurityContextStep(SecurityContextStatus.Completed, []));
        ScriptedSecurityContextFactory handBuilt = new(spnego);
        using ISecurityContext context = new RoutingSecurityContextFactory(isWindows: false, new ScriptedSecurityContextFactory(gss), handBuilt).Create(Explicit);

        SecurityContextStep first = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        SecurityContextStep second = await context.NextTokenAsync(new byte[] { 0x02 }, CancellationToken.None);

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
        ScriptedSecurityContext gss = new(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));
        ScriptedSecurityContextFactory handBuilt = new();
        using ISecurityContext context = new RoutingSecurityContextFactory(isWindows: false, new ScriptedSecurityContextFactory(gss), handBuilt).Create(Explicit);

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.AreEqual(SecurityContextStatus.NoCredentials, step.Status);
        Assert.IsEmpty(handBuilt.Requests);
    }

    [TestMethod]
    public async Task Create_NoMechanismOnALaterStep_DoesNotFallBack()
    {
        ScriptedSecurityContext gss = new(Token, new SecurityContextStep(SecurityContextStatus.NoMechanism, []));
        ScriptedSecurityContextFactory handBuilt = new();
        using ISecurityContext context = new RoutingSecurityContextFactory(isWindows: false, new ScriptedSecurityContextFactory(gss), handBuilt).Create(Explicit);

        await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        SecurityContextStep second = await context.NextTokenAsync(new byte[] { 0x02 }, CancellationToken.None);

        Assert.AreEqual(SecurityContextStatus.NoMechanism, second.Status);
        Assert.IsEmpty(handBuilt.Requests);
    }

    [TestMethod]
    public void Create_WindowsNegotiate_WrapsAndUnwrapsThroughSspi()
    {
        ScriptedSecurityContext sspi = new() { IsCompleted = true };
        using ISecurityContext context = new RoutingSecurityContextFactory(isWindows: true, new ScriptedSecurityContextFactory(sspi), new ScriptedSecurityContextFactory()).Create(Explicit);

        byte[]? wrapped = context.Wrap([0x01], encrypt: true);
        byte[]? unwrapped = context.Unwrap([(byte)'S', 0x02]);

        CollectionAssert.AreEqual(new byte[] { (byte)'E', 0x01 }, wrapped);
        CollectionAssert.AreEqual(new byte[] { 0x02 }, unwrapped);
    }

    [TestMethod]
    public async Task Create_GssApiUnsupportedOffWindows_WrapsAndUnwrapsOnTheHandBuiltRoute()
    {
        ScriptedSecurityContext gss = new(new SecurityContextStep(SecurityContextStatus.NoMechanism, []));
        ScriptedSecurityContext spnego = new(new SecurityContextStep(SecurityContextStatus.Completed, [])) { IsCompleted = true };
        using ISecurityContext context = new RoutingSecurityContextFactory(isWindows: false, new ScriptedSecurityContextFactory(gss), new ScriptedSecurityContextFactory(spnego)).Create(Explicit);
        await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        byte[]? wrapped = context.Wrap([0x01], encrypt: false);
        byte[]? unwrapped = context.Unwrap([]);

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
        RoutingSecurityContextFactory factory = new(isWindows: false, new ScriptedSecurityContextFactory(), new ScriptedSecurityContextFactory());

        Assert.ThrowsExactly<ArgumentNullException>(() => factory.Create(null!));
    }
}
