using Curl.Extension.Abstractions.Transfer;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.DnsQuery.Client;

/// <summary>
/// Gives a <c>dns://</c> transfer to the DNS query handler as a <see cref="DnsQueryContext" /> carrying the transport and
/// output format <c>--dns-transport</c> and <c>--dns-json</c> chose (ADR-1140). Every other transfer passes through unchanged.
/// </summary>
/// <param name="transport">The transport's name.</param>
/// <param name="json">Whether to write JSON.</param>
internal sealed class DnsQueryTransferHook(string transport, bool json) : ITransferHook
{
    /// <inheritdoc />
    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next) =>
        next(string.Equals(context.Url.Scheme, DnsQueryRequest.Scheme, StringComparison.OrdinalIgnoreCase) ? new DnsQueryContext(context, transport, json) : context);
}
