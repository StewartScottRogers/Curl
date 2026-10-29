namespace Curl.Extension.Abstractions.Transfer;

/// <summary>Gives <see cref="TransferHookServices" /> over a connector and a listener that fail the test if a hook uses them.</summary>
internal static class UnusedTransferHookServices
{
    public static TransferHookServices Create() => new(new UnusedConnector(), new UnusedListener());

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("The connector was used.");
    }

    private sealed class UnusedListener : IConnectionListener
    {
        public ValueTask<ListenResult> ListenAsync(ListenTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("The listener was used.");
    }
}
