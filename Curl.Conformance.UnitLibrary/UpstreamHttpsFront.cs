using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

// A case naming the https server (`https` or `https <certificate file>`) reaches it on %HTTPSPORT, which has a value only with a certificate directory.
internal sealed class UpstreamHttpsFront : UpstreamStandInFront
{
    public override UpstreamStandInStage Stage => UpstreamStandInStage.Sws;

    public override void AddVariables(Dictionary<string, string> variables, string? certificateDirectory)
    {
        if (certificateDirectory is not null)
        {
            variables["HTTPSPORT"] = UpstreamCaseRunner.HttpsPort;
        }
    }

    public override IConnector Wrap(IConnector inner, UpstreamStandInContext context) =>
        UpstreamCaseScreening.HttpsCertificateFile(context.TestCase) is { } certificateFile && context.CertificateDirectory is not null
            ? new HttpsServerConnector(context.Sws, context.LoadCertificate(certificateFile), context.Sws)
            : inner;
}
