using Curl.Conformance.Extended.AzureStorage;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Storage;

/// <summary>curl's generic transfer options (BL-4615) for <c>az://</c> against the reference Blob endpoint, reading a public container anonymously; each check is <see cref="StorageTransferChecks" />'s.</summary>
[TestClass]
public sealed class AzTransferCases
{
    private static readonly StorageTransferTarget Target = new(
        "acct.blob.core.windows.net",
        "az://acct/public/hello.txt",
        "az://acct/public/none.txt",
        "hello.txt",
        "hello, storage\n"u8.ToArray(),
        [],
        () =>
        {
            AzureBlobReferenceEndpoint endpoint = new(AzureStorageResponseFraming.ContentLength);
            endpoint.CreateContainer("public", anonymousRead: true);
            endpoint.StoreBlob("public", "hello.txt", "hello, storage\n"u8.ToArray());
            return endpoint;
        });

    [TestMethod]
    [SpecRequirement("CurlSurface:Verbose-Az-1")]
    public Task Verbose_Download_TellsTheConnectionAndTlsOnStandardErrorOnly() => StorageTransferChecks.VerboseAsync(Target, "CurlSurface:Verbose-Az-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:Output-Az-1")]
    public Task Output_ToAFile_WritesTheObjectThereAndNothingElse() => StorageTransferChecks.OutputFileAsync(Target, "CurlSurface:Output-Az-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:RemoteName-Az-1")]
    public Task RemoteName_IntoAnOutputDirectory_WritesTheObjectUnderTheUrlsLastSegment() => StorageTransferChecks.RemoteNameAsync(Target, "CurlSurface:RemoteName-Az-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:WriteOut-Az-1")]
    public Task WriteOut_AfterADownload_ReportsEachVariable() => StorageTransferChecks.WriteOutAsync(Target, "CurlSurface:WriteOut-Az-1", "az");

    [TestMethod]
    [SpecRequirement("CurlSurface:Fail-Az-1")]
    public Task Fail_MissingObject_Exits22WithNothingOnStandardOutput() => StorageTransferChecks.FailAsync(Target, "CurlSurface:Fail-Az-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:FailWithBody-Az-1")]
    public Task FailWithBody_MissingObject_Exits22AndWritesTheErrorBody() => StorageTransferChecks.FailWithBodyAsync(Target, "CurlSurface:FailWithBody-Az-1", "BlobNotFound");

    [TestMethod]
    [SpecRequirement("CurlSurface:MaxTime-Az-1")]
    public Task MaxTime_ServerNeverAnswers_Exits28AtTheDeadline() => StorageTransferChecks.MaxTimeAsync(Target, "CurlSurface:MaxTime-Az-1");
}
