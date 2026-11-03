using System.Buffers.Binary;
using System.Net;
using System.Text;
using Curl.Extended.Protocol.DnsQuery.Rendering;
using Curl.Extended.Protocol.DnsQuery.Transport;
using Curl.Extended.Protocol.DnsQuery.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.DnsQuery.Client;

/// <summary>
/// Serves <c>dns://[server[:port]]/name[?type=T;class=C]</c> (RFC 4501, ADR-1140): encodes one recursive query
/// (RFC 1035 section 4.1), sends it over the transport <c>--dns-transport</c> names (UDP by default), decodes the answer
/// and writes it as dig-like text, or as one line of JSON under <c>--dns-json</c>. A URL naming no server queries the
/// system's first DNS server. Any response code, NXDOMAIN included, is written and ends with exit 0, as dig does.
/// </summary>
/// <param name="services">Curl's connection services, which the transports open their connections through.</param>
/// <param name="systemDnsServers">Lists the system's DNS servers.</param>
/// <param name="randomBytes">Gives the given number of random bytes, for the message ID of a transport that does not query with ID 0.</param>
internal sealed class DnsQueryProtocolHandler(ProtocolHandlerServices services, Func<IReadOnlyList<IPEndPoint>> systemDnsServers, Func<int, byte[]> randomBytes) : IProtocolHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = [DnsQueryRequest.Scheme];

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            return await TransferAsync(context).ConfigureAwait(false);
        }
        catch (DnsQueryException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }
    }

    private async ValueTask<TransferResult> TransferAsync(ITransferContext context)
    {
        DnsQueryContext? options = context as DnsQueryContext;
        IDnsQueryTransport transport = DnsQueryTransportSelection.TransportOf(options?.Transport ?? DnsQueryTransportSelection.Udp, services);
        DnsQueryRequest request = DnsQueryRequest.Of(context.Url, transport.DefaultPort);
        ushort id = MessageIdFor(transport);
        byte[] query = Required(DnsQueryEncoder.Encode(id, request.Name, request.Type, request.Class, recursionDesired: true), CurlExitCode.UrlMalformat);
        (string host, int port) = ServerOf(request);
        context.Progress.ReportTransferStarted();
        byte[] answer = await transport.ExchangeAsync(host, port, query, context).ConfigureAwait(false);
        DnsMessage message = Required(DnsMessageDecoder.Decode(answer), CurlExitCode.WeirdServerReply);
        RequireQueryId(message, id);

        byte[] output = Encoding.UTF8.GetBytes(options?.Json == true ? DnsJsonRenderer.Render(message) + "\n" : DnsTextRenderer.Render(message));
        await context.Output.WriteAsync(output, context.CancellationToken).ConfigureAwait(false);
        return TransferResult.Success(output.Length);
    }

    /// <summary>Gives the query's message ID: 0 for a transport that queries with ID 0, otherwise a random one (RFC 5452 section 4).</summary>
    private ushort MessageIdFor(IDnsQueryTransport transport) =>
        transport.QueriesWithMessageIdZero ? (ushort)0 : BinaryPrimitives.ReadUInt16BigEndian(randomBytes(2));

    /// <summary>Fails with exit 8 when the answer does not carry the query's message ID (RFC 1035 section 4.1.1).</summary>
    private static void RequireQueryId(DnsMessage message, ushort id)
    {
        if (message.Header.Id != id)
        {
            throw new DnsQueryException(CurlExitCode.WeirdServerReply, $"The DNS server answered with message ID {message.Header.Id}, not the query's {id}");
        }
    }

    /// <summary>
    /// Gives the URL's server, or the system's first DNS server's address when the URL names none (RFC 4501 section 2),
    /// on the request's port, which is the transport's default when the URL names no server.
    /// </summary>
    private (string Host, int Port) ServerOf(DnsQueryRequest request)
    {
        if (request.Server is { } server)
        {
            return (server, request.Port);
        }

        IPEndPoint first = systemDnsServers().FirstOrDefault()
            ?? throw new DnsQueryException(CurlExitCode.CouldntResolveHost, "The dns: URL names no server and the system has no DNS server configured");
        return (first.Address.ToString(), request.Port);
    }

    private static T Required<T>(DnsOutcome<T> outcome, CurlExitCode exitCode)
        where T : class =>
        outcome.Value ?? throw new DnsQueryException(exitCode, outcome.Failure!);
}
