using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.DnsQuery.Client;

/// <summary>Carries one DNS query to a server and its answer back (ADR-1140).</summary>
internal interface IDnsQueryTransport
{
    /// <summary>Gets the port a URL that names no port is queried on over this transport.</summary>
    int DefaultPort { get; }

    /// <summary>Sends <paramref name="query" /> to the server and gives its answer.</summary>
    /// <param name="host">The server's host or address.</param>
    /// <param name="port">The server's port.</param>
    /// <param name="query">The encoded query.</param>
    /// <param name="context">The transfer, for its clock and cancellation.</param>
    /// <returns>The answer's octets.</returns>
    /// <exception cref="DnsQueryException">The server could not be reached or did not answer.</exception>
    ValueTask<byte[]> ExchangeAsync(string host, int port, byte[] query, ITransferContext context);
}
