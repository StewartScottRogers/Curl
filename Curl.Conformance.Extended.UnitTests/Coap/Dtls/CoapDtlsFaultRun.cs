using System.Security.Cryptography.X509Certificates;
using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Coap.Dtls;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.Coap.Dtls;

/// <summary>A <see cref="DtlsReferenceServer" /> with a fault, in front of a CoAP server that answers every request 2.05 "ok".</summary>
internal sealed class CoapDtlsFaultRun : IDisposable
{
    private readonly X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");

    public CoapDtlsFaultRun(DtlsReferenceFault fault)
    {
        Coap = CoapDtlsRun.CoapServer((request, _) => [CoapResponses.Piggybacked(request, 0x45, "ok"u8.ToArray())]);
        Server = new DtlsReferenceServer(Coap, certificate, fault);
    }

    public CoapReferenceServer Coap { get; }

    public DtlsReferenceServer Server { get; }

    public void Dispose()
    {
        Server.Dispose();
        certificate.Dispose();
    }
}
