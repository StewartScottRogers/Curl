using System.Net;
using Curl.Extension.ObliviousDoh.Encryption;
using Curl.Extension.ObliviousDoh.Http;
using Curl.Extension.ObliviousDoh.Wire;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.ObliviousDoh;

/// <summary>
/// Looks host names up over Oblivious DoH (RFC 9230): fetches the target's configs once, then for each name
/// encrypts an AAAA and an A query to the target's key and POSTs each through the proxy, returning the AAAA
/// addresses then the A addresses. Any failure gives no addresses, which the connector reports as exit 6.
/// </summary>
/// <param name="client">Sends the HTTP requests.</param>
/// <param name="endpoints">The proxy and the target.</param>
public sealed class ObliviousDohResolver(ObliviousDohHttpClient client, ObliviousDohEndpoints endpoints) : IDnsResolver
{
    private ObliviousDohConfig? config;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        if (IPAddress.TryParse(host.Trim('[', ']'), out IPAddress? literal))
        {
            return [literal];
        }

        config ??= await FetchConfigAsync(cancellationToken).ConfigureAwait(false);
        if (config is null)
        {
            return [];
        }

        IReadOnlyList<IPAddress> aaaa = await AskAsync(config, host, DnsRecordType.Aaaa, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<IPAddress> a = await AskAsync(config, host, DnsRecordType.A, cancellationToken).ConfigureAwait(false);
        return [.. aaaa, .. a];
    }

    private async ValueTask<ObliviousDohConfig?> FetchConfigAsync(CancellationToken cancellationToken)
    {
        ObliviousDohHttpResponse? response = await client.SendAsync(endpoints.ConfigsUri, null, cancellationToken).ConfigureAwait(false);
        return response is { StatusCode: 200 } ? ObliviousDohConfig.SelectFrom(response.Body) : null;
    }

    private async ValueTask<IReadOnlyList<IPAddress>> AskAsync(ObliviousDohConfig target, string host, DnsRecordType recordType, CancellationToken cancellationToken)
    {
        DnsQueryEncoding encoding = DnsQueryEncoder.Encode(host, recordType);
        ObliviousDohQuery? query = encoding.Failure == DnsMessageFailure.None ? ObliviousDohQuery.Seal(target, encoding.Bytes) : null;
        if (query is null)
        {
            return [];
        }

        using (query)
        {
            ObliviousDohHttpResponse? response = await client.SendAsync(endpoints.ProxyRequestUri, query.Message, cancellationToken).ConfigureAwait(false);
            byte[]? answer = response is { StatusCode: 200 } ? query.OpenResponse(response.Body) : null;
            return answer is null ? [] : DnsAnswerDecoder.Decode(answer, recordType).Addresses;
        }
    }
}
