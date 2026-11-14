namespace Curl.Protocol.Abstractions;

[TestClass]
public sealed class ProtocolHandlerChainFinishedTests
{
    [TestMethod]
    public async Task TransferChainFinishedAsync_HandlerWithoutTheMember_CompletesAtOnce()
    {
        IProtocolHandler handler = new PlainHandler();

        await handler.TransferChainFinishedAsync(new TransferContext { Url = CurlUrl.Parse("dict://x/d:a"), Output = Stream.Null }, TransferResult.Success(0));
    }

    private sealed class PlainHandler : IProtocolHandler
    {
        public IReadOnlyCollection<string> SupportedSchemes => ["dict"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) => ValueTask.FromResult(TransferResult.Success(0));
    }
}
