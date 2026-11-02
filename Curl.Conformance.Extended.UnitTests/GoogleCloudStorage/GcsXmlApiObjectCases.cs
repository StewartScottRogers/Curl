using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.GoogleCloudStorage;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.GoogleCloudStorage;

/// <summary>Cloud Storage XML API object operations through the curl command line: GET Object, PUT Object, errors, authorization and the endpoint.</summary>
[TestClass]
public sealed class GcsXmlApiObjectCases
{
    private const string XmlApiHost = "storage.googleapis.com";

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    private static readonly X509Certificate2 Certificate = ReferenceCertificate.CreateSelfSigned(XmlApiHost);

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    internal static Task<CurlRunResult> RunAgainstCloudStorageAsync(GcsReferenceServer server, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tls(443, server, Certificate)], ["-sS", "-k", .. arguments]);

    internal static string Text(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    [TestMethod]
    [SpecRequirement("GoogleCloudStorage:XMLAPI-GetObject-1")]
    [SpecRequirement("GoogleCloudStorage:XMLAPI-Endpoint-1")]
    [SpecRequirement("GoogleCloudStorage:XMLAPI-Headers-Host-1")]
    public Task Download_GetsThePathStyleObjectOverHttps() =>
        Known.RunCaseAsync(["GoogleCloudStorage:XMLAPI-GetObject-1", "GoogleCloudStorage:XMLAPI-Endpoint-1", "GoogleCloudStorage:XMLAPI-Headers-Host-1"], async () =>
        {
            GcsReferenceServer server = new(new GcsAnonymousReferenceAuthorizer());
            server.PutObject("photos", "2026/cat.txt", "meow\n"u8.ToArray());

            CurlRunResult result = await RunAgainstCloudStorageAsync(server, "gs://photos/2026/cat.txt");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual("meow\n", Text(result.StandardOutput));
            Assert.AreEqual(string.Empty, Text(result.StandardError));
            Assert.AreEqual("GET /photos/2026/cat.txt HTTP/1.1\r\nhost: storage.googleapis.com\r\n\r\n", Text(result.Exchanges.Single().ClientBytes));
        });

    [TestMethod]
    [SpecRequirement("GoogleCloudStorage:XMLAPI-PutObject-1")]
    [SpecRequirement("GoogleCloudStorage:XMLAPI-Headers-ContentLength-1")]
    public Task Upload_PutsTheFileAsTheObjectWithItsLength() =>
        Known.RunCaseAsync(["GoogleCloudStorage:XMLAPI-PutObject-1", "GoogleCloudStorage:XMLAPI-Headers-ContentLength-1"], async () =>
        {
            string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
            await File.WriteAllTextAsync(file, "hello gcs");
            try
            {
                GcsReferenceServer server = new(new GcsAnonymousReferenceAuthorizer());
                server.PutObject("drop", "keep", []);

                CurlRunResult result = await RunAgainstCloudStorageAsync(server, "-T", file, "gs://drop/in/hello.txt");

                Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
                Assert.AreEqual(string.Empty, Text(result.StandardOutput));
                Assert.AreEqual(string.Empty, Text(result.StandardError));
                Assert.AreEqual("PUT /drop/in/hello.txt HTTP/1.1\r\nhost: storage.googleapis.com\r\nContent-Length: 9\r\n\r\nhello gcs", Text(result.Exchanges.Single().ClientBytes));
                Assert.AreEqual("hello gcs", Text(server.GetObject("drop", "in/hello.txt")!));
            }
            finally
            {
                File.Delete(file);
            }
        });

    [TestMethod]
    [SpecRequirement("GoogleCloudStorage:XMLAPI-RequestURI-1")]
    public Task Download_PercentEncodesTheObjectName() =>
        Known.RunCaseAsync(["GoogleCloudStorage:XMLAPI-RequestURI-1"], async () =>
        {
            GcsReferenceServer server = new(new GcsAnonymousReferenceAuthorizer());
            server.PutObject("docs", "a b+c&d.txt", "spaced"u8.ToArray());

            CurlRunResult result = await RunAgainstCloudStorageAsync(server, "gs://docs/a%20b%2Bc%26d.txt");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual("spaced", Text(result.StandardOutput));
            Assert.AreEqual("/docs/a%20b%2Bc%26d.txt", server.Requests.Single().Target);
        });

    [TestMethod]
    [SpecRequirement("GoogleCloudStorage:XMLAPI-Errors-1")]
    public Task Download_ErrorBody_ExitsWith22AndNamesTheCode() =>
        Known.RunCaseAsync(["GoogleCloudStorage:XMLAPI-Errors-1"], async () =>
        {
            GcsReferenceServer server = new(new GcsAnonymousReferenceAuthorizer());
            server.PutObject("photos", "dog.txt", []);

            CurlRunResult result = await RunAgainstCloudStorageAsync(server, "gs://photos/cat.txt");

            Assert.AreEqual(22, result.ExitCode);
            Assert.AreEqual(string.Empty, Text(result.StandardOutput));
            Assert.AreEqual("curl: (22) Cloud Storage returned error 404: NoSuchKey: The specified key does not exist." + Environment.NewLine, Text(result.StandardError));
            StringAssert.StartsWith(Text(result.Exchanges.Single().ServerBytes), "HTTP/1.1 404 Not Found\r\n");
        });

    [TestMethod]
    [SpecRequirement("GoogleCloudStorage:XMLAPI-OAuth2-1")]
    public Task Download_OAuth2Bearer_SendsTheAccessToken() =>
        Known.RunCaseAsync(["GoogleCloudStorage:XMLAPI-OAuth2-1"], async () =>
        {
            GcsReferenceServer server = new(new GcsBearerReferenceAuthorizer("ya29.token"));
            server.PutObject("private", "secret.txt", "classified"u8.ToArray());

            CurlRunResult result = await RunAgainstCloudStorageAsync(server, "--oauth2-bearer", "ya29.token", "gs://private/secret.txt");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual("classified", Text(result.StandardOutput));
            Assert.AreEqual("GET /private/secret.txt HTTP/1.1\r\nhost: storage.googleapis.com\r\nAuthorization: Bearer ya29.token\r\n\r\n", Text(result.Exchanges.Single().ClientBytes));
        });

    [TestMethod]
    [SpecRequirement("GoogleCloudStorage:XMLAPI-Anonymous-1")]
    public Task Download_NoCredentials_SendsNoAuthorizationAndIsRefusedByAPrivateBucket() =>
        Known.RunCaseAsync(["GoogleCloudStorage:XMLAPI-Anonymous-1"], async () =>
        {
            GcsReferenceServer server = new(new GcsBearerReferenceAuthorizer("ya29.token"));
            server.PutObject("private", "secret.txt", "classified"u8.ToArray());

            CurlRunResult result = await RunAgainstCloudStorageAsync(server, "gs://private/secret.txt");

            Assert.AreEqual(22, result.ExitCode);
            Assert.AreEqual(string.Empty, Text(result.StandardOutput));
            Assert.AreEqual("curl: (22) Cloud Storage returned error 401: AuthenticationRequired: Authentication required." + Environment.NewLine, Text(result.StandardError));
            Assert.IsFalse(server.Requests.Single().Headers.ContainsKey("Authorization"));
        });

    [TestMethod]
    [SpecRequirement("GoogleCloudStorage:XMLAPI-Emulator-1")]
    public Task Download_StorageEmulatorHost_SendsTheRequestToTheEmulator() =>
        Known.RunCaseAsync(["GoogleCloudStorage:XMLAPI-Emulator-1"], async () =>
        {
            GcsReferenceServer server = new(new GcsAnonymousReferenceAuthorizer());
            server.PutObject("local", "a.txt", "emulated"u8.ToArray());

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(
                Curl, [ReferenceRoute.Tcp(9023, server)], ["-sS", "gs://local/a.txt"], new Dictionary<string, string> { ["STORAGE_EMULATOR_HOST"] = "http://localhost:9023" });

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual("emulated", Text(result.StandardOutput));
            WireExchange exchange = result.Exchanges.Single();
            Assert.AreEqual(9023, exchange.Port);
            Assert.AreEqual("GET /local/a.txt HTTP/1.1\r\nhost: localhost:9023\r\n\r\n", Text(exchange.ClientBytes));
        });
}
