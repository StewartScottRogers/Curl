using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Core.AltSvc;

/// <summary>
/// Writes <see cref="AltSvcCache" />'s work to Curl's own diagnostic log, component
/// <see cref="DiagnosticLogComponents.AltSvc" /> (ADR-0222, BL-1072): an alternative used as
/// <c>info</c>, each alternative stored and each origin with none as <c>verbose</c>.
/// </summary>
/// <param name="log">Where the lines go; <see cref="NoDiagnosticLog.Instance" /> writes nothing.</param>
/// <remarks>
/// Lines name versions, hosts and ports only; an alt-svc entry holds no credential. Every method
/// tests <see cref="IDiagnosticLog.IsEnabled" /> before it builds its message.
/// </remarks>
internal sealed class AltSvcDiagnosticLog(IDiagnosticLog log)
{
    /// <summary>Logs, at <c>verbose</c>, an alternative stored from an <c>Alt-Svc</c> header.</summary>
    /// <param name="entry">The entry stored.</param>
    public void Stored(AltSvcEntry entry)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            log.Write(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.AltSvc, $"stored alternative {Alternative(entry)}");
        }
    }

    /// <summary>Logs, at <c>info</c>, the alternative found for an origin and used.</summary>
    /// <param name="entry">The entry found.</param>
    public void Used(AltSvcEntry entry)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            log.Write(DiagnosticLogLevel.Info, DiagnosticLogComponents.AltSvc, $"using alternative {Alternative(entry)}");
        }
    }

    /// <summary>Logs, at <c>verbose</c>, an origin the cache holds no usable alternative for.</summary>
    /// <param name="host">The origin's host.</param>
    /// <param name="port">The origin's port.</param>
    public void Missed(string host, int port)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            log.Write(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.AltSvc, string.Create(
                CultureInfo.InvariantCulture,
                $"no alternative for {host}:{port}"));
        }
    }

    /// <summary>An entry as <c>h3 alt.example:443 for h2 example.com:443</c>.</summary>
    private static string Alternative(AltSvcEntry entry) => string.Create(
        CultureInfo.InvariantCulture,
        $"{AltSvcAlpnToken.Format(entry.DestinationAlpn)} {entry.DestinationHost}:{entry.DestinationPort} for {AltSvcAlpnToken.Format(entry.SourceAlpn)} {entry.SourceHost}:{entry.SourcePort}");
}
