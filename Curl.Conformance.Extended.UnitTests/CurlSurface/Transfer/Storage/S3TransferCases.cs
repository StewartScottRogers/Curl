using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.S3;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Storage;

/// <summary>curl's generic transfer options (BL-4615) for <c>s3://</c> against the reference S3 endpoint on its AWS host, virtual-hosted; each check is <see cref="StorageTransferChecks" />'s.</summary>
[TestClass]
public sealed class S3TransferCases
{
    private static readonly StorageTransferTarget Target = new(
        "photos.s3.us-east-1.amazonaws.com",
        "s3://photos/hello.txt",
        "s3://photos/none.txt",
        "hello.txt",
        "hello, storage\n"u8.ToArray(),
        [],
        () =>
        {
            S3ReferenceEndpoint endpoint = new("s3.us-east-1.amazonaws.com");
            endpoint.CreateBucket("photos");
            endpoint.PutObject("photos", "hello.txt", "hello, storage\n"u8.ToArray());
            return endpoint;
        });

    [TestMethod]
    [SpecRequirement("CurlSurface:Verbose-S3-1")]
    public Task Verbose_Download_TellsTheConnectionAndTlsOnStandardErrorOnly() => StorageTransferChecks.VerboseAsync(Target, "CurlSurface:Verbose-S3-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:Output-S3-1")]
    public Task Output_ToAFile_WritesTheObjectThereAndNothingElse() => StorageTransferChecks.OutputFileAsync(Target, "CurlSurface:Output-S3-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:RemoteName-S3-1")]
    public Task RemoteName_IntoAnOutputDirectory_WritesTheObjectUnderTheUrlsLastSegment() => StorageTransferChecks.RemoteNameAsync(Target, "CurlSurface:RemoteName-S3-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:WriteOut-S3-1")]
    public Task WriteOut_AfterADownload_ReportsEachVariable() => StorageTransferChecks.WriteOutAsync(Target, "CurlSurface:WriteOut-S3-1", "s3");

    [TestMethod]
    [SpecRequirement("CurlSurface:Fail-S3-1")]
    public Task Fail_MissingObject_Exits22WithNothingOnStandardOutput() => StorageTransferChecks.FailAsync(Target, "CurlSurface:Fail-S3-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:FailWithBody-S3-1")]
    public Task FailWithBody_MissingObject_Exits22AndWritesTheErrorBody() => StorageTransferChecks.FailWithBodyAsync(Target, "CurlSurface:FailWithBody-S3-1", "NoSuchKey");

    [TestMethod]
    [SpecRequirement("CurlSurface:MaxTime-S3-1")]
    public Task MaxTime_ServerNeverAnswers_Exits28AtTheDeadline() => StorageTransferChecks.MaxTimeAsync(Target, "CurlSurface:MaxTime-S3-1");
}
