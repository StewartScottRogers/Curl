using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.Redis;

/// <summary>
/// A Redis server written from the RESP specification: it reads each command curl sends as an array of bulk strings
/// (<see cref="RespRequestReader" />, which throws on anything else) and sends the reply <paramref name="respond" />
/// gives for it, one write per chunk, so a case can split a reply across reads. It ends when curl closes the connection.
/// </summary>
/// <param name="respond">Gives the reply chunks, raw RESP bytes, for a command's arguments.</param>
public sealed class RedisReferenceServer(Func<IReadOnlyList<byte[]>, IReadOnlyList<byte[]>> respond) : IReferenceServer
{
    /// <inheritdoc />
    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        RespRequestReader reader = new(connection);
        while (await reader.ReadCommandAsync(cancellationToken).ConfigureAwait(false) is { } command)
        {
            foreach (byte[] chunk in respond(command))
            {
                await connection.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
