using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// An <see cref="IDnsResolver" /> that stands for the run's <c>--doh-url</c> without being a
/// <see cref="DohDnsResolver" />, such as the resolver an unusable <c>--doh-url</c> leaves the run with.
/// When it answers nothing, <see cref="TcpConnector" /> writes the <c>-v</c> line
/// <c>Could not resolve host: &lt;host&gt;</c> once, as curl 8.21.0 does for every <c>--doh-url</c>,
/// not twice as its threaded resolver does (measured, BL-3916; ADR-0366).
/// </summary>
public interface IDohUrlDnsResolver : IDnsResolver;
