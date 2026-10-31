using System.Security.Cryptography.X509Certificates;
using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// curl 8.22.0's Schannel-build <c>-v</c> lines for the certificate check at the end of a
/// handshake: a port of the <c>infof</c> calls of <c>schannel_connect_step3</c> and
/// <c>add_certs_to_store</c> in <c>lib/vtls/schannel.c</c>, measured on Windows (BL-3754, CGR-225).
/// </summary>
internal static class SchannelVerificationText
{
    /// <summary>Returns the lines for a completed handshake.</summary>
    /// <param name="handshake">The facts the handshake negotiated.</param>
    /// <returns>
    /// The <c>added N certificate(s) from CA file</c> line when a <c>--cacert</c> file added any,
    /// then the <c>connection hostname ... validated</c> line when the host name was checked; none
    /// for a failed handshake.
    /// </returns>
    internal static IReadOnlyList<string> Lines(TlsHandshakeEvent handshake)
    {
        if (handshake.Failed)
        {
            return [];
        }

        return [.. CaCertificateLines(handshake), .. HostNameLines(handshake)];
    }

    private static IReadOnlyList<string> CaCertificateLines(TlsHandshakeEvent handshake) =>
        handshake.CaCertificateFile is { } file && handshake.CaCertificateCount > 0
            ? [$"schannel: added {handshake.CaCertificateCount} certificate(s) from CA file '{file}'"]
            : [];

    internal static IReadOnlyList<string> HostNameLines(TlsHandshakeEvent handshake)
    {
        if (handshake.VerifiedHostName is not { } host)
        {
            return [];
        }

        var name = handshake.ServerCertificate?.GetNameInfo(X509NameType.DnsName, false);
        return [$"schannel: connection hostname ({host}) validated against certificate name ({name ?? host})"];
    }
}
