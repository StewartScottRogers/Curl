using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

// A case naming the ftps server reaches it on %FTPSPORT, which has a value only with a certificate directory; stunnel presents test-localhost.pem.
internal sealed class UpstreamFtpsFront : UpstreamStandInFront
{
    public override UpstreamStandInStage Stage => UpstreamStandInStage.Pop3;

    public override void AddVariables(Dictionary<string, string> variables, string? certificateDirectory)
    {
        if (certificateDirectory is not null)
        {
            variables["FTPSPORT"] = UpstreamCaseRunner.FtpsPort;
        }
    }

    public override IConnector Wrap(IConnector inner, UpstreamStandInContext context) =>
        context.HasCertificateAndNamesAnyServer(FtpsServerConnector.EmulatedServers)
            ? new FtpsServerConnector(context.LoadDefaultCertificate(), inner)
            : inner;
}
