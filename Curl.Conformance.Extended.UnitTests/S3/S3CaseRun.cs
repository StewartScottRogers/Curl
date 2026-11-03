using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.S3;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.S3;

/// <summary>Runs one S3 case: the endpoint answers HTTPS on 443 (AWS's hosts, under <c>-k</c>) and HTTP on 9000 (a custom endpoint).</summary>
internal static class S3CaseRun
{
    internal const string AwsHost = "s3.us-east-1.amazonaws.com";

    internal const int CustomEndpointPort = 9000;

    internal static readonly KnownFailures KnownFailures = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    private static readonly X509Certificate2 Certificate = ReferenceCertificate.CreateSelfSigned(AwsHost);

    internal static S3ReferenceEndpoint EndpointWithBucket(string bucket, string serviceHost = AwsHost, int listPageSize = 1000, int? failingPartNumber = null, bool completionAnswersError = false)
    {
        S3ReferenceEndpoint endpoint = new(serviceHost) { ListPageSize = listPageSize, FailingPartNumber = failingPartNumber, CompletionAnswersError = completionAnswersError };
        endpoint.CreateBucket(bucket);
        return endpoint;
    }

    internal static Task<CurlRunResult> RunAsync(IReferenceServer endpoint, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment = null) =>
        ExtendedConformanceHarness.RunAsync(
            InProcessCurl.RunAsync,
            [ReferenceRoute.Tls(443, endpoint, Certificate), ReferenceRoute.Tcp(CustomEndpointPort, endpoint)],
            ["-sS", "-k", "-m", "60", .. arguments],
            environment);

    internal static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    internal static void AssertSucceeded(CurlRunResult result, string standardOutput)
    {
        Assert.AreEqual(string.Empty, Text(result.StandardError));
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(standardOutput, Text(result.StandardOutput));
    }
}
