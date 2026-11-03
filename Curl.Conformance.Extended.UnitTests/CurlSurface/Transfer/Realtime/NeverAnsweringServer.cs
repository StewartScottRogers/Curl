using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Realtime;

/// <summary>A server that reads what curl sends and never sends a byte; <see cref="Reached" /> completes at curl's first byte.</summary>
internal sealed class NeverAnsweringServer : IReferenceServer
{
    private readonly TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Reached => reached.Task;

    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[4096];
        while (await connection.ReadAsync(buffer, cancellationToken) > 0)
        {
            reached.TrySetResult();
        }
    }
}
