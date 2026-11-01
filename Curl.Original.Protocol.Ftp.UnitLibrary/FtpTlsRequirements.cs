using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Works out the <see cref="FtpTlsRequirement" /> a transfer asks for.
/// </summary>
internal static class FtpTlsRequirements
{
    /// <summary>
    /// Reads the requirement from <paramref name="context" />: <c>--ftp-ssl-control</c> first,
    /// since <see cref="ITransferContext.SslLevel" /> is <see cref="TransportSecurityLevel.Required" />
    /// under it too and the context leaves it unset under <c>--ssl-reqd</c> (curl 8.21.0 was
    /// measured to rank <c>--ssl-reqd</c> over <c>--ftp-ssl-control</c> over <c>--ssl</c>), then
    /// <c>--ssl-reqd</c>, then <c>--ssl</c>.
    /// </summary>
    /// <param name="context">The transfer.</param>
    /// <returns>The requirement.</returns>
    public static FtpTlsRequirement Of(ITransferContext context)
    {
        if (context.FtpSslControlOnly)
        {
            return FtpTlsRequirement.ControlConnection;
        }

        if (context.SslLevel == TransportSecurityLevel.Required)
        {
            return FtpTlsRequirement.AllConnections;
        }

        return context.SslLevel == TransportSecurityLevel.Try ? FtpTlsRequirement.Try : FtpTlsRequirement.None;
    }
}
