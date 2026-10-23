using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class TransferInfoFromReport
{
    /// <summary>The line a certificate's PEM block starts with.</summary>
    private const string PemBeginLine = "-----BEGIN CERTIFICATE-----";

    /// <summary>
    /// The <c>CURLINFO_CERTINFO</c> lists of the report's peer certificates, the server's own first; none unless
    /// <c>CURLOPT_CERTINFO</c> <paramref name="requested" /> them.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<string>> CertInfoOf(TransferReport report, bool requested) =>
        requested ? [.. report.PeerCertificates.Select(certificate => CertInfoRecordsOf(PeerCertificateText.Format(certificate)))] : [];

    /// <summary>
    /// One certificate's <c>CURLINFO_CERTINFO</c> list, from its <c>%{certs}</c> text (<see cref="PeerCertificateText" />):
    /// each <c>label:value</c> line is one string, and the PEM block, which <c>%{certs}</c> prints without the label,
    /// is libcurl's last string, <c>Cert:</c> and the block with its line feeds. Empty when the text is (a certificate
    /// curl could not read).
    /// </summary>
    /// <param name="certificateText">The certificate's <c>%{certs}</c> text.</param>
    /// <returns>The certificate's strings, in libcurl's order.</returns>
    internal static IReadOnlyList<string> CertInfoRecordsOf(string certificateText)
    {
        int pemStart = certificateText.IndexOf(PemBeginLine, StringComparison.Ordinal);
        return pemStart < 0
            ? []
            : [.. certificateText[..pemStart].Split('\n', StringSplitOptions.RemoveEmptyEntries), "Cert:" + certificateText[pemStart..]];
    }
}
