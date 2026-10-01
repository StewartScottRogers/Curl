using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Writes <see cref="ProxySelector" />'s choice to Curl's own diagnostic log, component
/// <see cref="DiagnosticLogComponents.Proxy" /> (ADR-0222, BL-1072): the proxy chosen as
/// <c>info</c>, and why none was as <c>verbose</c>.
/// </summary>
/// <param name="diagnosticLog">Where the lines go; <see langword="null" /> writes nothing.</param>
/// <remarks>
/// A proxy is written as its scheme, host and port only, never its user information, which may
/// hold a password (ADR-0222, decision 7). Every method tests <see cref="IDiagnosticLog.IsEnabled" />
/// before it builds its message.
/// </remarks>
internal sealed class ProxyDiagnosticLog(IDiagnosticLog? diagnosticLog)
{
    private readonly IDiagnosticLog log = diagnosticLog ?? NoDiagnosticLog.Instance;

    /// <summary>The scheme each <see cref="ProxyKind" /> is written with, indexed by its value.</summary>
    private static readonly string[] KindSchemes = ["http", "http", "https", "socks4", "socks4a", "socks5", "socks5h"];

    /// <summary>Logs, at <c>info</c>, the proxy chosen for a URL.</summary>
    /// <param name="url">The URL being fetched.</param>
    /// <param name="proxy">The proxy chosen.</param>
    public void Chosen(CurlUrl url, ProxyEndpoint proxy)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            log.Write(DiagnosticLogLevel.Info, DiagnosticLogComponents.Proxy, $"using proxy {Describe(proxy)} for {url.Scheme}://{url.Host}");
        }
    }

    /// <summary>Logs, at <c>verbose</c>, a host reached directly because a no-proxy entry exempts it.</summary>
    /// <param name="host">The URL's host.</param>
    /// <param name="entry">The no-proxy entry that matched it.</param>
    public void Exempted(string host, string entry) =>
        Direct(() => $"{host} matches no-proxy entry '{entry}'; connecting directly");

    /// <summary>Logs, at <c>verbose</c>, a URL reached directly for want of any proxy text.</summary>
    /// <param name="url">The URL being fetched.</param>
    public void NoneGiven(CurlUrl url) =>
        Direct(() => $"no proxy set for {url.Scheme}://{url.Host}; connecting directly");

    /// <summary>Logs, at <c>verbose</c>, a <c>file://</c> URL, which never uses a proxy.</summary>
    public void FileUrl() => Direct(() => "file URL never uses a proxy");

    /// <summary>A proxy as <c>scheme://host:port</c>, an IPv6 address in brackets.</summary>
    /// <param name="proxy">The proxy.</param>
    /// <returns>The text.</returns>
    internal static string Describe(ProxyEndpoint proxy)
    {
        string host = proxy.Host.Contains(':', StringComparison.Ordinal) ? $"[{proxy.Host}]" : proxy.Host;
        return string.Create(CultureInfo.InvariantCulture, $"{KindSchemes[(int)proxy.Kind]}://{host}:{proxy.Port}");
    }

    private void Direct(Func<string> build)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            log.Write(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Proxy, build());
        }
    }
}
