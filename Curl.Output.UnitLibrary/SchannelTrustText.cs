using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// curl 8.21.0's Schannel-build <c>-v</c> lines before a TLS handshake: a port of the
/// <c>infof</c> calls of <c>schannel_acquire_credential_handle</c> and
/// <c>schannel_connect_step1</c> in <c>lib/vtls/schannel.c</c>, measured on Windows (BL-1083).
/// </summary>
internal static class SchannelTrustText
{
    /// <summary>Returns the lines for the trust.</summary>
    /// <param name="trust">The trust.</param>
    /// <returns>The lines, without the <c>* </c> prefix.</returns>
    internal static IReadOnlyList<string> Lines(TlsTrustEvent trust)
    {
        var clientCertificateLine = trust.UsesAutomaticClientCertificate
            ? "schannel: enabled automatic use of client certificate"
            : "schannel: disabled automatic use of client certificate";
        return trust.TargetsIpAddress
            ? [clientCertificateLine, "schannel: using IP address, SNI is not supported by OS."]
            : [clientCertificateLine];
    }
}
