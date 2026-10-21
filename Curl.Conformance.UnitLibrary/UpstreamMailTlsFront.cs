using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

// A case naming an implicit-TLS mail server (smtps, imaps or pop3s) reaches it on its port, which has a value only with a certificate directory; stunnel presents test-localhost.pem.
internal sealed class UpstreamMailTlsFront : UpstreamStandInFront
{
    public override UpstreamStandInStage Stage => UpstreamStandInStage.Pop3;

    public override void AddVariables(Dictionary<string, string> variables, string? certificateDirectory)
    {
        if (certificateDirectory is not null)
        {
            variables["SMTPSPORT"] = UpstreamCaseRunner.SmtpsPort;
            variables["IMAPSPORT"] = UpstreamCaseRunner.ImapsPort;
            variables["POP3SPORT"] = UpstreamCaseRunner.Pop3sPort;
        }
    }

    public override IConnector Wrap(IConnector inner, UpstreamStandInContext context) =>
        context.HasCertificateAndNamesAnyServer(MailTlsServerConnector.EmulatedServers)
            ? new MailTlsServerConnector(context.LoadDefaultCertificate(), inner)
            : inner;
}
