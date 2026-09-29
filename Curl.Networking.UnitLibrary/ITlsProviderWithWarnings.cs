using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// An <see cref="ITlsProvider" /> that also names the lines curl writes to standard error for
/// options its build ignores, so the console prints them whichever TLS client
/// <see cref="TlsClientRouting" /> chose: <see cref="SslStreamTlsProvider" /> or
/// <see cref="HandBuiltTlsProvider" />.
/// </summary>
public interface ITlsProviderWithWarnings : ITlsProvider
{
    /// <summary>
    /// Gets the lines curl writes to standard error, unless <c>-s</c> is given, for options
    /// the build ignores; each line is without its line ending.
    /// </summary>
    IReadOnlyList<string> Warnings { get; }
}
