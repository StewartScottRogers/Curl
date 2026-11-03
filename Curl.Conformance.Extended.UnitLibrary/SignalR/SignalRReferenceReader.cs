using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.SignalR;

/// <summary>Reads whole HTTP requests and WebSocket frames, one after another, from one reference hub connection.</summary>
/// <param name="connection">The connection.</param>
internal sealed class SignalRReferenceReader(ReferenceConnection connection)
{
    private byte[] pending = [];

    /// <summary>Reads one whole item from what curl sent a piece at a time.</summary>
    /// <typeparam name="T">The item read.</typeparam>
    /// <param name="parse">Reads an item from the start of the bytes held, giving the bytes it took, 0 when it is not whole yet.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The item, or <see langword="null" /> once curl closed the connection before a whole one arrived.</returns>
    public async Task<T?> ReadAsync<T>(SignalRReferenceParser<T> parse, CancellationToken cancellationToken)
        where T : class
    {
        while (true)
        {
            int taken = parse(pending, out T? item);
            if (taken > 0)
            {
                pending = pending[taken..];
                return item;
            }

            byte[] buffer = new byte[4096];
            int read = await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            pending = [.. pending, .. buffer.AsSpan(0, read)];
        }
    }
}
