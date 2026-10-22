using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the default members of <see cref="ISaslExchange" />, which let an implementation
/// leave them out.
/// </summary>
[TestClass]
public sealed class ISaslExchangeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void CancelReason_WhenNotOverridden_ReturnsNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        ISaslExchange exchange = new MinimalSaslExchange();
        diagnostics.Arrange("mechanism", exchange.Mechanism);

        var cancelReason = exchange.CancelReason;

        diagnostics.Act("cancel reason", cancelReason);
        diagnostics.Assert("cancel reason", null, cancelReason);
        Assert.IsNull(cancelReason);
    }

    [TestMethod]
    public void SendsLastAnswerAsEmptyLine_WhenNotOverridden_ReturnsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        ISaslExchange exchange = new MinimalSaslExchange();
        diagnostics.Arrange("mechanism", exchange.Mechanism);

        bool sendsEmptyLine = exchange.SendsLastAnswerAsEmptyLine;

        diagnostics.Act("sends last answer as empty line", sendsEmptyLine);
        diagnostics.Assert("sends last answer as empty line", false, sendsEmptyLine);
        Assert.IsFalse(sendsEmptyLine);
    }

    private sealed class MinimalSaslExchange : ISaslExchange
    {
        public string Mechanism => "PLAIN";

        public ValueTask<byte[]?> GetInitialResponseAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<byte[]?> RespondAsync(ReadOnlyMemory<byte> challenge, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
