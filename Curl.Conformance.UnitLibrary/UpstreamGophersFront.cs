using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

// A case naming the gophers server reaches it on %GOPHERSPORT, which has a value only with a certificate directory; stunnel presents test-localhost.pem.
internal sealed class UpstreamGophersFront : UpstreamStandInFront
{
    public override UpstreamStandInStage Stage => UpstreamStandInStage.Gopher;

    public override void AddVariables(Dictionary<string, string> variables, string? certificateDirectory)
    {
        if (certificateDirectory is not null)
        {
            variables["GOPHERSPORT"] = UpstreamCaseRunner.GophersPort;
        }
    }

    public override IConnector Wrap(IConnector inner, UpstreamStandInContext context) =>
        context.HasCertificateAndNamesAnyServer(GophersServerConnector.EmulatedServers)
            ? new GophersServerConnector(context.LoadDefaultCertificate(), inner)
            : inner;
}
