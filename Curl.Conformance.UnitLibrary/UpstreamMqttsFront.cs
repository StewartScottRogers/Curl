using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

// A case naming the mqtts server reaches it on %MQTTSPORT, which has a value only with a certificate directory; stunnel presents test-localhost.pem.
internal sealed class UpstreamMqttsFront : UpstreamStandInFront
{
    public override UpstreamStandInStage Stage => UpstreamStandInStage.Mqtt;

    public override void AddVariables(Dictionary<string, string> variables, string? certificateDirectory)
    {
        if (certificateDirectory is not null)
        {
            variables["MQTTSPORT"] = UpstreamCaseRunner.MqttsPort;
        }
    }

    public override IConnector Wrap(IConnector inner, UpstreamStandInContext context) =>
        context.HasCertificateAndNamesAnyServer(MqttsServerConnector.EmulatedServers)
            ? new MqttsServerConnector(context.LoadDefaultCertificate(), inner)
            : inner;
}
