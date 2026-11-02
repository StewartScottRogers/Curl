using Curl.Conformance.Extended.GoogleCloudStorage;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.GoogleCloudStorage;

/// <summary>Cloud Storage XML API List Objects through the curl command line: <c>list-type=2</c>, <c>prefix</c>, <c>delimiter</c> and paging.</summary>
[TestClass]
public sealed class GcsXmlApiListCases
{
    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    private static GcsReferenceServer BucketOfFive(GcsListPagination pagination)
    {
        GcsReferenceServer server = new(new GcsAnonymousReferenceAuthorizer(), pageSize: 2, pagination);
        foreach (string name in new[] { "logs/a.txt", "logs/b.txt", "logs/deep/c.txt", "logs/deep/d.txt", "logs/e.txt", "other.txt" })
        {
            server.PutObject("site", name, [1, 2, 3]);
        }

        return server;
    }

    [TestMethod]
    [SpecRequirement("GoogleCloudStorage:XMLAPI-ListObjects-1")]
    [SpecRequirement("GoogleCloudStorage:XMLAPI-ListObjects-2")]
    [SpecRequirement("GoogleCloudStorage:XMLAPI-ListObjects-3")]
    public Task List_WithDelimiterOverPages_FollowsTheContinuationToken() =>
        Known.RunCaseAsync(["GoogleCloudStorage:XMLAPI-ListObjects-1", "GoogleCloudStorage:XMLAPI-ListObjects-2", "GoogleCloudStorage:XMLAPI-ListObjects-3"], async () =>
        {
            GcsReferenceServer server = BucketOfFive(GcsListPagination.NextContinuationToken);

            CurlRunResult result = await GcsXmlApiObjectCases.RunAgainstCloudStorageAsync(server, "gs://site/logs/");

            Assert.AreEqual(0, result.ExitCode, GcsXmlApiObjectCases.Text(result.StandardError));
            Assert.AreEqual("logs/a.txt\nlogs/b.txt\nlogs/deep/\nlogs/e.txt\n", GcsXmlApiObjectCases.Text(result.StandardOutput));
            Assert.AreEqual(string.Empty, GcsXmlApiObjectCases.Text(result.StandardError));
            Assert.HasCount(2, result.Exchanges);
            Assert.AreEqual("/site?list-type=2&delimiter=%2F&prefix=logs%2F", server.Requests[0].Target);
            Assert.AreEqual("/site?list-type=2&delimiter=%2F&prefix=logs%2F&continuation-token=bG9ncy9iLnR4dA%3D%3D", server.Requests[1].Target);
        });

    [TestMethod]
    [SpecRequirement("GoogleCloudStorage:XMLAPI-ListObjects-4")]
    public Task List_NextMarkerPages_FollowsTheMarker() =>
        Known.RunCaseAsync(["GoogleCloudStorage:XMLAPI-ListObjects-4"], async () =>
        {
            GcsReferenceServer server = BucketOfFive(GcsListPagination.NextMarker);

            CurlRunResult result = await GcsXmlApiObjectCases.RunAgainstCloudStorageAsync(server, "gs://site/");

            Assert.AreEqual(0, result.ExitCode, GcsXmlApiObjectCases.Text(result.StandardError));
            Assert.AreEqual("logs/\nother.txt\n", GcsXmlApiObjectCases.Text(result.StandardOutput));
            Assert.HasCount(1, server.Requests);
            Assert.AreEqual("/site?list-type=2&delimiter=%2F&prefix=", server.Requests[0].Target);
        });

    [TestMethod]
    [SpecRequirement("GoogleCloudStorage:XMLAPI-ListObjects-4")]
    public Task List_NextMarkerOverSeveralPages_SendsTheMarkerBack() =>
        Known.RunCaseAsync(["GoogleCloudStorage:XMLAPI-ListObjects-4"], async () =>
        {
            GcsReferenceServer server = BucketOfFive(GcsListPagination.NextMarker);

            CurlRunResult result = await GcsXmlApiObjectCases.RunAgainstCloudStorageAsync(server, "gs://site/logs/");

            Assert.AreEqual(0, result.ExitCode, GcsXmlApiObjectCases.Text(result.StandardError));
            Assert.AreEqual("logs/a.txt\nlogs/b.txt\nlogs/deep/\nlogs/e.txt\n", GcsXmlApiObjectCases.Text(result.StandardOutput));
            Assert.AreEqual("/site?list-type=2&delimiter=%2F&prefix=logs%2F&continuation-token=logs%2Fb.txt", server.Requests[1].Target);
        });
}
