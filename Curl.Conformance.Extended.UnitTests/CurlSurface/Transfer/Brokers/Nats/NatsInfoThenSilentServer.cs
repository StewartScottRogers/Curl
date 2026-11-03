using System.Text;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Brokers.Nats;

/// <summary>
/// A NATS server that sends its INFO line and then reads what curl sends without ever answering it, so curl waits for
/// the PONG that ends its handshake; <see cref="Reached" /> completes at curl's first byte.
/// </summary>
internal sealed class NatsInfoThenSilentServer : IReferenceServer
{
    private readonly TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Reached => reached.Task;

    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        await connection.WriteAsync(Encoding.ASCII.GetBytes("INFO {}\r\n"), cancellationToken);
        byte[] buffer = new byte[4096];
        while (await connection.ReadAsync(buffer, cancellationToken) > 0)
        {
            reached.TrySetResult();
        }
    }
}
