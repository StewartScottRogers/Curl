using Curl.Extended.Protocol.DnsQuery.Client;

namespace Curl.Extended.Protocol.DnsQuery.Transport.Https;

/// <summary>
/// A failed send or receive on the DoH server's connection, carrying how the query ends. It is an
/// <see cref="IOException" /> so the HTTP/2 layer treats it as a transport failure: a GOAWAY that cannot be sent after a
/// protocol error leaves the protocol error as the outcome.
/// </summary>
/// <param name="failure">How the query ends: curl's exit code and words.</param>
internal sealed class DohConnectionException(DnsQueryException failure) : IOException(failure.Message, failure)
{
    /// <summary>Gets how the query ends: curl's exit code and words.</summary>
    public DnsQueryException Failure { get; } = failure;
}
