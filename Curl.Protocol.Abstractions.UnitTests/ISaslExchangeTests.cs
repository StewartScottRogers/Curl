namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the default members of <see cref="ISaslExchange" />, which let an implementation
/// leave them out.
/// </summary>
[TestClass]
public sealed class ISaslExchangeTests
{
    [TestMethod]
    public void CancelReason_WhenNotOverridden_ReturnsNull()
    {
        ISaslExchange exchange = new MinimalSaslExchange();

        var cancelReason = exchange.CancelReason;

        Assert.IsNull(cancelReason);
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
