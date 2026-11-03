using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.S3;

namespace Curl.Conformance.Extended.UnitTests.S3;

[TestClass]
public sealed class S3ListingCases
{
    private static S3ReferenceEndpoint EndpointWithKeys(string bucket, int listPageSize)
    {
        S3ReferenceEndpoint endpoint = S3CaseRun.EndpointWithBucket(bucket, listPageSize: listPageSize);
        foreach (string key in new[] { "a.txt", "dir/x", "dir/y", "e.txt", "other/z" })
        {
            endpoint.PutObject(bucket, key, [1]);
        }

        return endpoint;
    }

    [TestMethod]
    [SpecRequirement("S3:ListObjectsV2-1")]
    [SpecRequirement("S3:ListObjectsV2-3")]
    [SpecRequirement("S3:ListObjectsV2-4")]
    public async Task List_OverSeveralPagesWithADelimiter_FollowsEachContinuationToken()
    {
        S3ReferenceEndpoint endpoint = EndpointWithKeys("logs", 2);

        CurlRunResult result = await S3CaseRun.RunAsync(endpoint, ["s3://logs/"]);

        S3CaseRun.AssertSucceeded(result, "dir/\na.txt\nother/\ne.txt\n");
        CollectionAssert.AreEqual(
            new[] { "/?list-type=2&delimiter=%2F&prefix=", "/?list-type=2&delimiter=%2F&prefix=&continuation-token=token-2" },
            endpoint.Requests.Select(request => request.Target).ToArray());
    }

    [TestMethod]
    [SpecRequirement("S3:ListObjectsV2-2")]
    public async Task List_KeyEndingInSlash_ListsUnderThatPrefix()
    {
        S3ReferenceEndpoint endpoint = EndpointWithKeys("logs", 1000);

        CurlRunResult result = await S3CaseRun.RunAsync(endpoint, ["s3://logs/dir/"]);

        S3CaseRun.AssertSucceeded(result, "dir/x\ndir/y\n");
        Assert.AreEqual("dir/", endpoint.Requests.Single().Query["prefix"]);
    }
}
