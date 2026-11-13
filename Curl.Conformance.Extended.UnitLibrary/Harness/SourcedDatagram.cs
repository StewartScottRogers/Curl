using System.Net;

namespace Curl.Conformance.Extended.Harness;

/// <summary>One datagram a reference server sends back, and the endpoint it appears to come from.</summary>
/// <param name="Datagram">The datagram's octets.</param>
/// <param name="Source">The endpoint the datagram comes from, or <see langword="null" /> for the server's own endpoint.</param>
public sealed record SourcedDatagram(ReadOnlyMemory<byte> Datagram, EndPoint? Source);
