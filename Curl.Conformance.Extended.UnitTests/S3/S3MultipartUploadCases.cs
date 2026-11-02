using System.Security.Cryptography;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.S3;

namespace Curl.Conformance.Extended.UnitTests.S3;

[TestClass]
public sealed class S3MultipartUploadCases
{
    // The client's part size (8 MiB) plus a short last part.
    private const int PartSize = 8 * 1024 * 1024;

    private static async Task<(CurlRunResult Result, byte[] Content)> UploadAsync(S3ReferenceEndpoint endpoint)
    {
        byte[] content = new byte[PartSize + 5];
        new Random(4498).NextBytes(content);
        DirectoryInfo directory = Directory.CreateTempSubdirectory();
        try
        {
            string file = Path.Combine(directory.FullName, "big.bin");
            await File.WriteAllBytesAsync(file, content);
            return (await S3CaseRun.RunAsync(endpoint, ["-T", file, "s3://backups/big.bin"]), content);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    private static string ETagOf(byte[] part) => "\"" + Convert.ToHexStringLower(MD5.HashData(part)) + "\"";

    [TestMethod]
    [SpecRequirement("S3:CreateMultipartUpload-1")]
    [SpecRequirement("S3:UploadPart-1")]
    [SpecRequirement("S3:UploadPart-2")]
    [SpecRequirement("S3:CompleteMultipartUpload-1")]
    [SpecRequirement("S3:CompleteMultipartUpload-2")]
    public async Task Upload_FromThePartSize_CreatesUploadsPartsAndCompletesInOrder()
    {
        S3ReferenceEndpoint endpoint = S3CaseRun.EndpointWithBucket("backups");

        (CurlRunResult result, byte[] content) = await UploadAsync(endpoint);

        S3CaseRun.AssertSucceeded(result, string.Empty);
        IReadOnlyList<S3ReferenceRequest> requests = endpoint.Requests;
        CollectionAssert.AreEqual(
            new[] { "POST /big.bin?uploads=", "PUT /big.bin?partNumber=1&uploadId=upload-1", "PUT /big.bin?partNumber=2&uploadId=upload-1", "POST /big.bin?uploadId=upload-1" },
            requests.Select(request => request.Method + " " + request.Target).ToArray());
        Assert.HasCount(PartSize, requests[1].Body);
        Assert.HasCount(5, requests[2].Body);
        Assert.AreEqual(
            "<CompleteMultipartUpload xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\">"
            + $"<Part><PartNumber>1</PartNumber><ETag>{ETagOf(content[..PartSize])}</ETag></Part>"
            + $"<Part><PartNumber>2</PartNumber><ETag>{ETagOf(content[PartSize..])}</ETag></Part>"
            + "</CompleteMultipartUpload>",
            S3CaseRun.Text(requests[3].Body));
        CollectionAssert.AreEqual(content, endpoint.FindObject("backups", "big.bin"));
    }

    [TestMethod]
    [SpecRequirement("S3:UploadPart-3")]
    public async Task Upload_PartFailing_StopsWithoutCompletingAndExits22()
    {
        S3ReferenceEndpoint endpoint = S3CaseRun.EndpointWithBucket("backups", failingPartNumber: 2);

        (CurlRunResult result, _) = await UploadAsync(endpoint);

        Assert.AreEqual(22, result.ExitCode);
        StringAssert.Contains(S3CaseRun.Text(result.StandardError), "S3 returned error 500: InternalError");
        Assert.HasCount(3, endpoint.Requests);
        Assert.IsNull(endpoint.FindObject("backups", "big.bin"));
    }

    [TestMethod]
    [SpecRequirement("S3:CompleteMultipartUpload-3")]
    public async Task Upload_CompletionAnswering200WithAnErrorBody_Exits22()
    {
        S3ReferenceEndpoint endpoint = S3CaseRun.EndpointWithBucket("backups", completionAnswersError: true);

        (CurlRunResult result, _) = await UploadAsync(endpoint);

        Assert.AreEqual(22, result.ExitCode);
        StringAssert.Contains(S3CaseRun.Text(result.StandardError), "InternalError");
        Assert.IsNull(endpoint.FindObject("backups", "big.bin"));
    }
}
