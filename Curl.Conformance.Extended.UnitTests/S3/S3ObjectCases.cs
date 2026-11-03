using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.S3;

namespace Curl.Conformance.Extended.UnitTests.S3;

[TestClass]
public sealed class S3ObjectCases
{
    [TestMethod]
    [SpecRequirement("S3:GetObject-1")]
    [SpecRequirement("S3:VirtualHosting-1")]
    [SpecRequirement("S3:CommonHeaders-1")]
    public async Task Download_GetsTheKeyFromTheVirtualHostedBucket()
    {
        S3ReferenceEndpoint endpoint = S3CaseRun.EndpointWithBucket("photos");
        endpoint.PutObject("photos", "2026/cat.txt", "meow"u8.ToArray());

        CurlRunResult result = await S3CaseRun.RunAsync(endpoint, ["s3://photos/2026/cat.txt"]);

        S3CaseRun.AssertSucceeded(result, "meow");
        S3ReferenceRequest request = endpoint.Requests.Single();
        Assert.AreEqual("GET", request.Method);
        Assert.AreEqual("/2026/cat.txt", request.Target);
        Assert.AreEqual("photos.s3.us-east-1.amazonaws.com", request.Headers["Host"]);
        Assert.IsTrue(request.VirtualHosted);
        Assert.AreEqual(443, result.Exchanges.Single().Port);
    }

    [TestMethod]
    [SpecRequirement("S3:PutObject-1")]
    [SpecRequirement("S3:PutObject-2")]
    public async Task Upload_BelowThePartSize_IsOnePutWithItsLength()
    {
        S3ReferenceEndpoint endpoint = S3CaseRun.EndpointWithBucket("photos");
        DirectoryInfo directory = Directory.CreateTempSubdirectory();
        try
        {
            string file = Path.Combine(directory.FullName, "note.txt");
            await File.WriteAllTextAsync(file, "hello s3");

            CurlRunResult result = await S3CaseRun.RunAsync(endpoint, ["-T", file, "s3://photos/notes/note.txt"]);

            S3CaseRun.AssertSucceeded(result, string.Empty);
            S3ReferenceRequest request = endpoint.Requests.Single();
            Assert.AreEqual("PUT", request.Method);
            Assert.AreEqual("/notes/note.txt", request.Target);
            Assert.AreEqual("8", request.Headers["Content-Length"]);
            Assert.AreEqual("hello s3", S3CaseRun.Text(endpoint.FindObject("photos", "notes/note.txt")!));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [TestMethod]
    [SpecRequirement("S3:ObjectKeys-1")]
    public async Task Download_KeyWithSpaceAndPlus_IsPercentEncodedOnce()
    {
        S3ReferenceEndpoint endpoint = S3CaseRun.EndpointWithBucket("photos");
        endpoint.PutObject("photos", "my file+1.txt", "x"u8.ToArray());

        CurlRunResult result = await S3CaseRun.RunAsync(endpoint, ["s3://photos/my%20file+1.txt"]);

        S3CaseRun.AssertSucceeded(result, "x");
        Assert.AreEqual("/my%20file%2B1.txt", endpoint.Requests.Single().Target);
    }

    [TestMethod]
    [SpecRequirement("S3:ErrorResponses-1")]
    public async Task Download_MissingKey_ReportsTheErrorCodeAndExits22()
    {
        S3ReferenceEndpoint endpoint = S3CaseRun.EndpointWithBucket("photos");

        CurlRunResult result = await S3CaseRun.RunAsync(endpoint, ["s3://photos/none.txt"]);

        Assert.AreEqual(22, result.ExitCode);
        Assert.AreEqual(string.Empty, S3CaseRun.Text(result.StandardOutput));
        StringAssert.Contains(S3CaseRun.Text(result.StandardError), "S3 returned error 404: NoSuchKey: The specified key does not exist.");
        StringAssert.Contains(Encoding.UTF8.GetString(result.Exchanges.Single().ServerBytes), "<Error><Code>NoSuchKey</Code>");
    }

    [TestMethod]
    [SpecRequirement("S3:ErrorResponses-2")]
    public async Task Download_MissingBucket_Exits22()
    {
        S3ReferenceEndpoint endpoint = S3CaseRun.EndpointWithBucket("photos");

        CurlRunResult result = await S3CaseRun.RunAsync(endpoint, ["s3://videos/a.txt"]);

        Assert.AreEqual(22, result.ExitCode);
        StringAssert.Contains(S3CaseRun.Text(result.StandardError), "S3 returned error 404: NoSuchBucket");
    }
}
