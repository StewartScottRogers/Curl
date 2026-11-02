using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.S3;

namespace Curl.Conformance.Extended.UnitTests.S3;

[TestClass]
public sealed class S3AddressingCases
{
    [TestMethod]
    [SpecRequirement("S3:VirtualHosting-2")]
    [SpecRequirement("S3:VirtualHosting-3")]
    public async Task List_BucketWithDots_IsPathStyleOnTheRegionalHost()
    {
        S3ReferenceEndpoint endpoint = S3CaseRun.EndpointWithBucket("my.bucket");
        endpoint.PutObject("my.bucket", "k", [1]);

        CurlRunResult result = await S3CaseRun.RunAsync(endpoint, ["s3://my.bucket/"]);

        S3CaseRun.AssertSucceeded(result, "k\n");
        S3ReferenceRequest request = endpoint.Requests.Single();
        Assert.AreEqual(S3CaseRun.AwsHost, request.Headers["Host"]);
        Assert.AreEqual("/my.bucket?list-type=2&delimiter=%2F&prefix=", request.Target);
        Assert.IsFalse(request.VirtualHosted);
    }

    [TestMethod]
    [SpecRequirement("S3:Endpoints-1")]
    public Task CustomEndpoint_FromAwsEndpointUrl_IsPathStyleOverHttp() =>
        S3CaseRun.KnownFailures.RunCaseAsync(["S3:Endpoints-1"], async () =>
        {
            S3ReferenceEndpoint endpoint = S3CaseRun.EndpointWithBucket("data", serviceHost: "localhost");
            endpoint.PutObject("data", "a.txt", "minio"u8.ToArray());
            Dictionary<string, string> environment = new() { ["AWS_ENDPOINT_URL"] = "http://localhost:9000" };

            CurlRunResult result = await S3CaseRun.RunAsync(endpoint, ["s3://data/a.txt"], environment);

            S3CaseRun.AssertSucceeded(result, "minio");
            Assert.AreEqual(S3CaseRun.CustomEndpointPort, result.Exchanges.Single().Port);
            Assert.AreEqual("localhost:9000", endpoint.Requests.Single().Headers["Host"]);
            Assert.AreEqual("/data/a.txt", endpoint.Requests.Single().Target);
        });

    [TestMethod]
    [SpecRequirement("S3:Regions-1")]
    public Task Region_FromAwsRegion_NamesTheRegionalHost() =>
        S3CaseRun.KnownFailures.RunCaseAsync(["S3:Regions-1"], async () =>
        {
            S3ReferenceEndpoint endpoint = S3CaseRun.EndpointWithBucket("data", serviceHost: "s3.eu-west-2.amazonaws.com");
            endpoint.PutObject("data", "a.txt", "london"u8.ToArray());
            Dictionary<string, string> environment = new() { ["AWS_REGION"] = "eu-west-2" };

            CurlRunResult result = await S3CaseRun.RunAsync(endpoint, ["s3://data/a.txt"], environment);

            S3CaseRun.AssertSucceeded(result, "london");
            Assert.AreEqual("data.s3.eu-west-2.amazonaws.com", endpoint.Requests.Single().Headers["Host"]);
        });
}
