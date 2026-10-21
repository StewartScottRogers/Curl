using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

// A case naming the https-proxy server reaches it on %HTTPSPROXYPORT, which has a value only with a certificate directory; stunnel presents test-localhost.pem.
internal sealed class UpstreamHttpsProxyFront : UpstreamStandInFront
{
    public override UpstreamStandInStage Stage => UpstreamStandInStage.Outermost;

    public override void AddVariables(Dictionary<string, string> variables, string? certificateDirectory)
    {
        if (certificateDirectory is not null)
        {
            variables["HTTPSPROXYPORT"] = UpstreamCaseRunner.HttpsProxyPort;
        }
    }

    public override IConnector Wrap(IConnector inner, UpstreamStandInContext context) =>
        context.HasCertificateAndNamesAnyServer([HttpsProxyServerConnector.EmulatedServer])
            ? new HttpsProxyServerConnector(context.LoadDefaultCertificate(), inner)
            : inner;
}
