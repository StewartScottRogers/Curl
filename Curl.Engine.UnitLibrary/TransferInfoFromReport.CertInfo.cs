using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class TransferInfoFromReport
{
    /// <summary>The line a certificate's PEM block starts with.</summary>
    private const string PemBeginLine = "-----BEGIN CERTIFICATE-----";

    /// <summary>
    /// The <c>CURLINFO_CERTINFO</c> lists of the report's peer certificates, the server's own first, in the layout of
    /// <paramref name="tlsBackend" />'s curl build (the platform's when null); none unless <c>CURLOPT_CERTINFO</c> <paramref name="requested" /> them.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<string>> CertInfoOf(TransferReport report, bool requested, TlsBackend? tlsBackend)
    {
        TlsBackend backend = tlsBackend ?? PlatformTlsBackend.ForProcess;
        return requested ? [.. report.PeerCertificates.Select(certificate => CertInfoRecordsOf(PeerCertificateText.FormatFor(certificate, backend), backend))] : [];
    }

    /// <summary>
    /// One certificate's <c>CURLINFO_CERTINFO</c> list, from its <c>%{certs}</c> text (<see cref="PeerCertificateText" />):
    /// each <c>label:value</c> line is one string, and the PEM block, which <c>%{certs}</c> prints without the label,
    /// is libcurl's last string, <c>Cert:</c> and the block with its line feeds. The OpenSSL build's authority key
    /// identifier spans several lines (<c>keyid:</c>, <c>DirName:</c>, <c>serial:</c>), which stay one string with their
    /// line feeds, as libcurl keeps them. Empty when the text is (a certificate curl could not read).
    /// </summary>
    /// <param name="certificateText">The certificate's <c>%{certs}</c> text.</param>
    /// <param name="tlsBackend">The TLS library whose curl build laid the text out.</param>
    /// <returns>The certificate's strings, in libcurl's order.</returns>
    internal static IReadOnlyList<string> CertInfoRecordsOf(string certificateText, TlsBackend tlsBackend)
    {
        int pemStart = certificateText.IndexOf(PemBeginLine, StringComparison.Ordinal);
        if (pemStart < 0)
        {
            return [];
        }

        List<string> records = [];
        foreach (string line in certificateText[..pemStart].Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (tlsBackend == TlsBackend.OpenSsl && records.Count > 0 && IsKeyIdentifierContinuation(line))
            {
                records[^1] += "\n" + line;
            }
            else
            {
                records.Add(line);
            }
        }

        records.Add("Cert:" + certificateText[pemStart..]);
        return records;
    }

    private static bool IsKeyIdentifierContinuation(string line) =>
        line.StartsWith("DirName:", StringComparison.Ordinal) || line.StartsWith("serial:", StringComparison.Ordinal);
}
