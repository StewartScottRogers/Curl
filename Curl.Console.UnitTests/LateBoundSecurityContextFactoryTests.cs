using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="LateBoundSecurityContextFactory" />: it refuses to make a context before a
/// factory is bound, and makes each one with the factory bound last.
/// </summary>
[TestClass]
public sealed class LateBoundSecurityContextFactoryTests
{
    private static readonly SecurityContextRequest Request = new(SecurityMechanism.Ntlm, "HTTP", "proxy.test");

    [TestMethod]
    public void Create_BeforeAnyFactoryIsBound_Throws()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => new LateBoundSecurityContextFactory().Create(Request));
    }

    [TestMethod]
    public void Create_AfterBind_MakesTheContextWithTheBoundFactory()
    {
        RecordingFactory bound = new();
        LateBoundSecurityContextFactory factory = new();
        factory.Bind(bound);

        ISecurityContext context = factory.Create(Request);

        Assert.AreSame(bound.Made, context);
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
