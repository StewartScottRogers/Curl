using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

// nghttpx relays each stream to sws as HTTP/1.1, so the relayed requests land in the sws emulation's ReceivedBytes.
// Its TLS front on %HTTP2TLSPORT presents test-localhost.pem, loaded only for a case naming the http/2 server when a certificate directory is named (BL-2245).
internal sealed class UpstreamHttp2Front(UpstreamHttp2Server? http2Server) : UpstreamStandInFront
{
    public override UpstreamStandInStage Stage => UpstreamStandInStage.Outermost;

    public override void AddVariables(Dictionary<string, string> variables, string? certificateDirectory)
    {
        if (certificateDirectory is not null)
        {
            variables["HTTP2TLSPORT"] = UpstreamCaseRunner.Http2TlsPort;
        }
    }

    public override IConnector Wrap(IConnector inner, UpstreamStandInContext context) =>
        http2Server?.InFrontOf(inner, context.Sws, context.HasCertificateAndNamesAnyServer(["http/2"]) ? context.LoadDefaultCertificate() : null) ?? inner;
}
