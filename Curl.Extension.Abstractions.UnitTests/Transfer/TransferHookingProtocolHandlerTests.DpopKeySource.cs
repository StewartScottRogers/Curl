namespace Curl.Extension.Abstractions.Transfer;

public sealed partial class TransferHookingProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_WithAHookHoldingADpopKey_LendsItToTheOtherHooksBesideTheConnectorAndListener()
    {
        TransferHookServices services = UnusedTransferHookServices.Create();
        TokenHook hook = new();
        KeyHoldingHook keyHolder = new();

        await new TransferHooks([hook, keyHolder], services).Around(new HeaderRecordingHandler()).ExecuteAsync(ContextWith(new MemoryStream(), null, null));

        Assert.AreSame(keyHolder, hook.Services!.DpopKeySource);
        Assert.AreSame(services.Connector, hook.Services.Connector);
        Assert.AreSame(services.Listener, hook.Services.Listener);
        Assert.IsNull(services.DpopKeySource);
    }

    private sealed class KeyHoldingHook : IDpopKeySource
    {
        public ValueTask<IDpopKey?> ReadDpopKeyAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IDpopKey?>(null);
    }
}
