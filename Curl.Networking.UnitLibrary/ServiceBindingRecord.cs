using System.Net;

namespace Curl.Networking;

/// <summary>
/// One HTTPS (or SVCB) record's data as RFC 9460 defines it, decoded by
/// <see cref="ServiceBindingRecordDecoder" />: the priority, the target name and the SvcParams
/// curl reads (<c>alpn</c>, <c>no-default-alpn</c>, <c>port</c>, <c>ipv4hint</c>, <c>ech</c>,
/// <c>ipv6hint</c>). A parameter the record does not carry keeps its empty default.
/// </summary>
/// <param name="Priority">The SvcPriority: 0 for AliasMode, otherwise ServiceMode's preference.</param>
/// <param name="TargetName">The TargetName, dotted, without the root's trailing dot; <c>.</c> for the root name itself.</param>
public sealed record ServiceBindingRecord(ushort Priority, string TargetName)
{
    /// <summary>Gets the <c>alpn</c> protocol identifiers, in record order.</summary>
    public IReadOnlyList<string> ApplicationProtocols { get; init; } = [];

    /// <summary>Gets a value indicating whether the record carries <c>no-default-alpn</c>.</summary>
    public bool NoDefaultApplicationProtocol { get; init; }

    /// <summary>Gets the <c>port</c> parameter, or <see langword="null" /> when the record has none.</summary>
    public ushort? Port { get; init; }

    /// <summary>Gets the <c>ipv4hint</c> addresses, in record order.</summary>
    public IReadOnlyList<IPAddress> IPv4Hints { get; init; } = [];

    /// <summary>Gets the <c>ipv6hint</c> addresses, in record order.</summary>
    public IReadOnlyList<IPAddress> IPv6Hints { get; init; } = [];

    /// <summary>Gets the <c>ech</c> parameter's ECHConfigList bytes; empty when the record has none.</summary>
    public ReadOnlyMemory<byte> EchConfigList { get; init; }
}
