using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

// A case naming the https-mtls server reaches it on %HTTPS-MTLSPORT, which has a value only with a certificate directory:
// stunnel in front of sws presenting test-localhost.pem and demanding a client certificate the test CA signed (BL-2859).
// Registered after UpstreamHttpsFront, whose connector does not pass on to the one it wraps.
internal sealed class UpstreamHttpsMtlsFront : UpstreamStandInFront
{
    public const string EmulatedServer = "https-mtls";

    public const string AuthorityFile = "test-ca.crt";

    public override UpstreamStandInStage Stage => UpstreamStandInStage.Sws;

    public override void AddVariables(Dictionary<string, string> variables, string? certificateDirectory)
    {
        if (certificateDirectory is not null)
        {
            variables["HTTPS-MTLSPORT"] = HttpsServerConnector.HttpsMtlsPort.ToString(CultureInfo.InvariantCulture);
        }
    }

    public override IConnector Wrap(IConnector inner, UpstreamStandInContext context) =>
        context.HasCertificateAndNamesAnyServer([EmulatedServer])
            ? new HttpsServerConnector(
                context.Sws,
                context.LoadDefaultCertificate(),
                inner,
                HttpsServerConnector.HttpsMtlsPort,
                new TlsClientCertificateVerifier(X509CertificateLoader.LoadCertificateFromFile(Path.Combine(context.CertificateDirectory!, "certs", AuthorityFile))))
            : inner;
}
