namespace Curl.Networking;

/// <summary>The outcome of <see cref="DnsQueryEncoder.Encode" />.</summary>
/// <param name="Bytes">The DNS query message; empty when <paramref name="Failure" /> is not <see cref="DnsMessageFailure.None" />.</param>
/// <param name="Failure">
/// <see cref="DnsMessageFailure.None" />, <see cref="DnsMessageFailure.BadLabel" /> or
/// <see cref="DnsMessageFailure.NameTooLong" />.
/// </param>
public sealed record DnsQueryEncoding(byte[] Bytes, DnsMessageFailure Failure);
