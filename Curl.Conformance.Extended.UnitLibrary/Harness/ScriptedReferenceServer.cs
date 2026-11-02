using System.Runtime.InteropServices;

namespace Curl.Conformance.Extended.Harness;

/// <summary>
/// A reference server that replays a fixed script on every connection - waits for curl's bytes and
/// sends canned ones - after which the harness closes the connection and keeps what curl sent. A
/// wait that sees curl close the connection ends the script there.
/// </summary>
/// <param name="steps">The script, in order.</param>
public sealed class ScriptedReferenceServer(params IReadOnlyList<ScriptStep> steps) : IReferenceServer
{
    /// <inheritdoc />
    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        foreach (ScriptStep step in steps)
        {
            if (step.Sends)
            {
                await connection.WriteAsync(step.Bytes, cancellationToken).ConfigureAwait(false);
            }
            else if (!await ReceiveUntilAsync(connection, step.Bytes, cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    // Reads until the bytes read end with the terminator; false when curl closed the connection first.
    private static async Task<bool> ReceiveUntilAsync(ReferenceConnection connection, ReadOnlyMemory<byte> terminator, CancellationToken cancellationToken)
    {
        List<byte> received = [];
        byte[] buffer = new byte[1];
        while (!EndsWith(received, terminator.Span))
        {
            if (await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false) == 0)
            {
                return false;
            }

            received.Add(buffer[0]);
        }

        return true;
    }

    private static bool EndsWith(List<byte> received, ReadOnlySpan<byte> terminator) =>
        received.Count >= terminator.Length
        && CollectionsMarshal.AsSpan(received)[^terminator.Length..].SequenceEqual(terminator);
}
