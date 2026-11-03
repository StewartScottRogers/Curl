using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.OciRegistry;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.OciRegistry;

/// <summary>Runs curl against one <see cref="OciReferenceRegistry" /> serving the registry on 443, blob storage and uploads on 8443 and the token endpoint on 9443.</summary>
internal static class OciRegistryRuns
{
    public const string Accept =
        "application/vnd.oci.image.manifest.v1+json, application/vnd.oci.image.index.v1+json, "
        + "application/vnd.docker.distribution.manifest.v2+json, application/vnd.docker.distribution.manifest.list.v2+json";

    public static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    private static readonly X509Certificate2 Certificate = ReferenceCertificate.CreateSelfSigned("registry.example");

    public static Task<CurlRunResult> RunAsync(OciReferenceRegistry registry, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(
            Curl,
            [ReferenceRoute.Tls(443, registry, Certificate), ReferenceRoute.Tls(8443, registry, Certificate), ReferenceRoute.Tls(9443, registry, Certificate)],
            ["-sS", "-k", .. arguments]);

    public static async Task<CurlRunResult> PushAsync(OciReferenceRegistry registry, byte[] upload, string url)
    {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bin");
        await File.WriteAllBytesAsync(file, upload);
        try
        {
            return await RunAsync(registry, "-T", file, url);
        }
        finally
        {
            File.Delete(file);
        }
    }

    public static string Text(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    public static string[] Requests(CurlRunResult result) => [.. result.Exchanges.Select(exchange => Text(exchange.ClientBytes))];

    public static string Get(string target, string host = "registry.example", string accept = Accept, string? authorization = null) =>
        $"GET {target} HTTP/1.1\r\nHost: {host}\r\nAccept: {accept}\r\n{(authorization is null ? string.Empty : $"Authorization: {authorization}\r\n")}Connection: close\r\n\r\n";
}
