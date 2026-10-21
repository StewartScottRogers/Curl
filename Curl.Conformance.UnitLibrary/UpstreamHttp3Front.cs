using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

// nghttpx-h3 relays each request to sws as HTTP/1.1, so the relayed requests land in the sws emulation's ReceivedBytes.
// It presents test-localhost.pem, loaded only for a case naming the http/3 server when a certificate directory is named (BL-2261).
// nghttpx-h3 takes only datagrams on %HTTP3PORT, so --http3's TCP leg is refused there and the race settles on QUIC (ADR-0498).
internal sealed class UpstreamHttp3Front(UpstreamHttp3Server http3Server) : UpstreamStandInFront
{
    public override UpstreamStandInStage Stage => UpstreamStandInStage.Outermost;

    // runtests.pl reports nghttpx-h3 when it finds the nghttpx that serves HTTP/3, which the stand-in replaces.
    public override UpstreamCurlPlatform WithFeatures(UpstreamCurlPlatform platform) => platform.WithFeatures([UpstreamCaseRunner.Http3ServerFeature]);

    public override void AddVariables(Dictionary<string, string> variables, string? certificateDirectory)
    {
        if (certificateDirectory is not null)
        {
            variables["HTTP3PORT"] = UpstreamCaseRunner.Http3Port;
        }
    }

    public override IConnector Wrap(IConnector inner, UpstreamStandInContext context) => UpstreamHttp3Server.RefusingTcpOnHttp3Port(inner);

    public override IDatagramConnector WrapDatagram(IDatagramConnector inner, UpstreamStandInContext context) =>
        context.HasCertificateAndNamesAnyServer(["http/3"])
            ? http3Server.InFrontOf(inner, context.Sws, context.LoadDefaultCertificate())
            : inner;
}
