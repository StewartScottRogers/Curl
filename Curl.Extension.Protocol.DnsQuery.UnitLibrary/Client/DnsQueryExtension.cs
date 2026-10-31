using System.Security.Cryptography;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Protocol.DnsQuery.Transport;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.DnsQuery.Client;

/// <summary>The <c>dns://</c> extension's description, options, transfer hook and handler factory (ADR-1140).</summary>
public static class DnsQueryExtension
{
    /// <summary>Gets <c>--dns-transport</c>, which names the transport a <c>dns://</c> query goes over.</summary>
    public static ExtensionOption TransportOption { get; } = new(
        "dns-transport",
        ExtensionOptionArgument.Value,
        $"Send a dns:// query over the named transport: {string.Join(", ", DnsQueryTransportSelection.Names)} (default {DnsQueryTransportSelection.Udp}, retried over TCP when the answer is truncated)");

    /// <summary>Gets <c>--dns-json</c>, which writes a <c>dns://</c> answer as JSON in place of text.</summary>
    public static ExtensionOption JsonOption { get; } = new(
        "dns-json",
        ExtensionOptionArgument.None,
        "Write a dns:// answer as one line of JSON in place of dig-like text");

    /// <summary>Gets what <c>curl --extensions</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "dns",
        "Queries a DNS server with a dns:// URL (RFC 4501): dns://server[:port]/name?type=A;class=IN sends an RFC 1035 query over UDP, retried over TCP when the answer is truncated, or over TCP with --dns-transport tcp, and writes the answer as dig-like text, or as JSON with --dns-json.",
        [DnsQueryRequest.Scheme],
        [TransportOption, JsonOption],
        new Uri("https://www.rfc-editor.org/rfc/rfc4501"));

    /// <summary>Creates the handler over curl's connectors, with the system's DNS servers for a URL that names none and system randomness for message IDs.</summary>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The handler.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new DnsQueryProtocolHandler(services, SystemDnsServers.List, RandomNumberGenerator.GetBytes);
    }

    /// <summary>Gives the hook that hands <c>--dns-transport</c> and <c>--dns-json</c> to the handler when either is given, otherwise <see langword="null" />.</summary>
    /// <param name="values">An option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" />.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.IsGiven(TransportOption) || values.IsGiven(JsonOption)
            ? new DnsQueryTransferHook(values.ValueOf(TransportOption) ?? DnsQueryTransportSelection.Udp, values.IsGiven(JsonOption))
            : null;
    }
}
