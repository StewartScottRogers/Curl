using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="LateBoundSecurityContextFactory" />: it refuses to make a context before a
/// factory is bound, and makes each one with the factory bound last.
/// </summary>
[TestClass]
public sealed class LateBoundSecurityContextFactoryTests
{
    private static readonly SecurityContextRequest Request = new(SecurityMechanism.Ntlm, "HTTP", "proxy.test");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Create_BeforeAnyFactoryIsBound_Throws()
    {
        Diagnostics.Arrange("request", "Ntlm HTTP proxy.test, no factory bound");

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => new LateBoundSecurityContextFactory().Create(Request));

        Diagnostics.Act("exception", exception.GetType().Name);

        Diagnostics.Assert("exception type", typeof(InvalidOperationException), exception.GetType());
    }

    [TestMethod]
    public void Create_AfterBind_MakesTheContextWithTheBoundFactory()
    {
        RecordingFactory bound = new();
        LateBoundSecurityContextFactory factory = new();
        factory.Bind(bound);
        Diagnostics.Arrange("request", "Ntlm HTTP proxy.test, factory bound");

        ISecurityContext context = factory.Create(Request);

        Diagnostics.Act("context is the bound factory's", ReferenceEquals(bound.Made, context));
        Diagnostics.Act("request reached the bound factory", ReferenceEquals(Request, bound.Requested));

        Diagnostics.Assert("context", true, ReferenceEquals(bound.Made, context));
        Assert.AreSame(bound.Made, context);
        Diagnostics.Assert("requested", true, ReferenceEquals(Request, bound.Requested));
        Assert.AreSame(Request, bound.Requested);
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
