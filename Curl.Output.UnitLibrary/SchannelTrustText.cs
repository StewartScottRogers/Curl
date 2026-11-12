using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// curl 8.21.0's Schannel-build <c>-v</c> lines before a TLS handshake: a port of the
/// <c>infof</c> calls of <c>schannel_acquire_credential_handle</c> and
/// <c>schannel_connect_step1</c> in <c>lib/vtls/schannel.c</c>, measured on Windows (BL-1083).
/// </summary>
internal static class SchannelTrustText
{
    // Measured: curl 8.22.0 (Schannel) -v --ciphers CALG_AES_128 (BL-4992, ADR-1250).
    private const string LessSecureTlsWarning =
        "schannel: WARNING: This version of Schannel negotiates a less-secure TLS version than TLS 1.3 because the user set an algorithm cipher list.";

    /// <summary>Returns the lines for the trust.</summary>
    /// <param name="trust">The trust.</param>
    /// <returns>The lines, without the <c>* </c> prefix.</returns>
    internal static IReadOnlyList<string> Lines(TlsTrustEvent trust)
    {
        if (trust.ReusesCredentialHandle)
        {
            return ["schannel: reusing existing credential handle", "schannel: incremented credential handle refcount = 2"];
        }

        var clientCertificateLine = trust.UsesAutomaticClientCertificate
            ? "schannel: enabled automatic use of client certificate"
            : "schannel: disabled automatic use of client certificate";
        List<string> lines = [clientCertificateLine];
        if (trust.HasCipherList)
        {
            lines.Add(LessSecureTlsWarning);
        }

        if (trust.TargetsIpAddress)
        {
            lines.Add("schannel: using IP address, SNI is not supported by OS.");
        }

        return lines;
    }
}
