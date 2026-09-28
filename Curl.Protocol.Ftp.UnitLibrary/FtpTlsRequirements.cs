using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Works out the <see cref="FtpTlsRequirement" /> a transfer asks for.
/// </summary>
internal static class FtpTlsRequirements
{
    /// <summary>
    /// Reads the requirement from <paramref name="context" />: <c>--ssl-reqd</c> wins over
    /// <c>--ftp-ssl-control</c>, which wins over <c>--ssl</c>, as curl 8.21.0 was measured to
    /// rank them.
    /// </summary>
    /// <param name="context">The transfer.</param>
    /// <returns>The requirement.</returns>
    public static FtpTlsRequirement Of(ITransferContext context)
    {
        if (context.SslLevel == TransportSecurityLevel.Required)
        {
            return FtpTlsRequirement.AllConnections;
        }

        if (context.FtpSslControlOnly)
        {
            return FtpTlsRequirement.ControlConnection;
        }

        return context.SslLevel == TransportSecurityLevel.Try ? FtpTlsRequirement.Try : FtpTlsRequirement.None;
    }
}
