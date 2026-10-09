using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="HandBuiltNtlmSecurityContextFactory" />: NTLM goes to the hand-built factory
/// and every other mechanism to the router (BL-1858).
/// </summary>
[TestClass]
public sealed class HandBuiltNtlmSecurityContextFactoryTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(SecurityMechanism.Ntlm, true)]
    [DataRow(SecurityMechanism.Negotiate, false)]
    [DataRow(SecurityMechanism.Kerberos, false)]
    public void Create_Mechanism_GoesToTheHandBuiltFactoryOnlyForNtlm(SecurityMechanism mechanism, bool expectedHandBuilt)
    {
        RecordingFactory router = new();
        RecordingFactory handBuilt = new();
        SecurityContextRequest request = new(mechanism, "HTTP", "proxy.test");
        Diagnostics.Arrange("mechanism", mechanism);

        ISecurityContext context = new HandBuiltNtlmSecurityContextFactory(router, handBuilt).Create(request);

        bool madeByHandBuilt = ReferenceEquals(handBuilt.Made, context);
        Diagnostics.Act("context is the hand-built factory's", madeByHandBuilt);

        Diagnostics.Assert("hand-built", expectedHandBuilt, madeByHandBuilt);
        Assert.AreSame(expectedHandBuilt ? handBuilt.Made : router.Made, context);
        Assert.AreSame(request, expectedHandBuilt ? handBuilt.Requested : router.Requested);
    }

    [TestMethod]
    public void Create_NullRequest_Throws()
    {
        Diagnostics.Arrange("request", "null");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => new HandBuiltNtlmSecurityContextFactory(new RecordingFactory(), new RecordingFactory()).Create(null!));

        Diagnostics.Act("exception", exception.GetType().Name);

        Diagnostics.Assert("parameter", "request", exception.ParamName);
        Assert.AreEqual("request", exception.ParamName);
    }

    private sealed class RecordingFactory : ISecurityContextFactory
    {
        public SecurityContextRequest? Requested { get; private set; }

        public ISecurityContext Made { get; } = new NoContext();

        public ISecurityContext Create(SecurityContextRequest request)
        {
            Requested = request;
            return Made;
        }
    }

    private sealed class NoContext : ISecurityContext
    {
        public bool IsCompleted => false;

        public ValueTask<SecurityContextStep> NextTokenAsync(ReadOnlyMemory<byte> incomingToken, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public byte[]? Wrap(ReadOnlySpan<byte> message, bool encrypt) => throw new NotSupportedException();

        public byte[]? Unwrap(ReadOnlySpan<byte> wrappedMessage) => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
