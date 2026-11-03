using System.Text;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Storage;

/// <summary>A server that reads curl's HTTP request to its blank line, then reads on and never sends a byte.</summary>
internal sealed class SilentStorageServer : IReferenceServer
{
    private readonly TaskCompletionSource requestReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes once a whole request head has arrived.</summary>
    public Task RequestReceived => requestReceived.Task;

    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        StringBuilder received = new();
        byte[] buffer = new byte[1];
        while (await connection.ReadAsync(buffer, cancellationToken) == 1)
        {
            received.Append((char)buffer[0]);
            if (received.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
            {
                requestReceived.TrySetResult();
            }
        }
    }
}
