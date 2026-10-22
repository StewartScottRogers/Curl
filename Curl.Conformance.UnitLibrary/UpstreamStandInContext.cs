using System.Security.Cryptography.X509Certificates;

namespace Curl.Conformance;

/// <summary>What a stand-in front needs to know about the case it is built for.</summary>
/// <param name="TestCase">The parsed case.</param>
/// <param name="CertificateDirectory">The folder holding upstream's <c>certs</c> folder, or <see langword="null"/> when none is named.</param>
/// <param name="Sws">The case's <c>sws</c> emulation.</param>
internal sealed record UpstreamStandInContext(UpstreamTestCase TestCase, string? CertificateDirectory, SwsHttpServerConnector Sws)
{
    /// <summary>Whether a certificate directory is named and the case lists a server in <paramref name="emulatedServers"/>.</summary>
    /// <param name="emulatedServers">The server names to look for.</param>
    /// <returns><see langword="true"/> when stunnel would present a certificate for the case.</returns>
    public bool HasCertificateAndNamesAnyServer(IEnumerable<string> emulatedServers) =>
        CertificateDirectory is not null && UpstreamTestPartBodies.Lines(TestCase.Find("client", "server")).Intersect(emulatedServers).Any();

    /// <summary>Loads <c>test-localhost.pem</c> from the certificate directory, which must be named.</summary>
    /// <returns>The certificate stunnel presents by default.</returns>
    public X509Certificate2 LoadDefaultCertificate() => LoadCertificate(HttpsServerConnector.DefaultCertificateFile);

    /// <summary>Loads a certificate file from the certificate directory's <c>certs</c> folder, which must be named.</summary>
    /// <param name="certificateFile">The file name in <c>certs</c>.</param>
    /// <returns>The certificate.</returns>
    public X509Certificate2 LoadCertificate(string certificateFile) =>
        HttpsServerConnector.LoadCertificate(Path.Combine(CertificateDirectory!, "certs", certificateFile));
}
