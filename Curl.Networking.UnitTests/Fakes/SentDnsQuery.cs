using System.Net;

namespace Curl.Networking.Fakes;

/// <summary>One query <see cref="ScriptedDnsSocketOpener" /> saw sent.</summary>
/// <param name="Transport"><c>udp</c> or <c>tcp</c>.</param>
/// <param name="Server">The server it went to.</param>
/// <param name="LocalAddress">The local address its socket was bound to.</param>
/// <param name="Query">The query bytes, without TCP's length prefix.</param>
/// <param name="SentAt">The time provider's timestamp when it was sent, in its units.</param>
public sealed record SentDnsQuery(string Transport, IPEndPoint Server, IPAddress LocalAddress, byte[] Query, long SentAt);
