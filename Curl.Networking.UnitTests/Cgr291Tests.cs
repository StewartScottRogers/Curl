using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// CGR-291 (BL-3820): the OpenSSL build sends <c>status_request</c> only with <c>--cert-status</c>.
/// Measured on 2026-10-28 against <c>openssl s_server -status_file ...</c>, which prints
/// <c>cert_status: callback called</c> only when the ClientHello carries <c>status_request</c>:
/// <c>curl -sS --cacert rca.crt https://localhost:p/index.txt</c> (and the same with <c>-k</c>)
/// prints no such line.
/// </summary>
public sealed partial class HandBuiltTlsProviderTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AuthenticateAsClientAsync_WithoutCertStatusInTheOpenSslBuild_SendsNoStatusRequest(bool insecure)
    {
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("insecure", insecure);

        byte[] record;
        using (Diagnostics.Phase("TLS handshake"))
        {
            record = await CaptureClientHelloAsync(new TlsClientOptions(Insecure: insecure), OpenSslBuild, ProfileHost, Http11);
        }

        var types = ExtensionTypes(DecodeClientHello(record));
        Diagnostics.Bytes("captured ClientHello record", record);
        Diagnostics.Assert("extension types", "no status_request", string.Join(",", types));
        CollectionAssert.DoesNotContain(types, TlsExtensionType.StatusRequest);
    }
}
