using System.Security.Authentication;

namespace Curl.Cli;

/// <summary>
/// The TLS 1.0 and TLS 1.1 <see cref="SslProtocols"/> members, which .NET marks obsolete
/// (<c>SYSLIB0039</c>) but curl still lets a user ask for through <c>-1</c>/<c>--tlsv1</c>,
/// <c>--tlsv1.0</c>, <c>--tlsv1.1</c>, <c>--tls-max 1.0</c>, <c>--tls-max 1.1</c> and
/// <c>--proxy-tlsv1</c>.
/// </summary>
/// <remarks>
/// This is the one place in <c>Curl.Cli.UnitLibrary</c> that suppresses <c>SYSLIB0039</c>:
/// everything else names these two constants, so the analyzer stays on everywhere else.
/// Whether the operating system's TLS stack will still negotiate them is the TLS provider's
/// question, not the parser's.
/// </remarks>
public static class ObsoleteTlsProtocols
{
#pragma warning disable SYSLIB0039 // curl still offers TLS 1.0 and 1.1 on request; see the remarks.
    /// <summary>TLS 1.0, <see cref="SslProtocols.Tls"/>.</summary>
    public const SslProtocols Tls10 = SslProtocols.Tls;

    /// <summary>TLS 1.1, <see cref="SslProtocols.Tls11"/>.</summary>
    public const SslProtocols Tls11 = SslProtocols.Tls11;
#pragma warning restore SYSLIB0039
}
