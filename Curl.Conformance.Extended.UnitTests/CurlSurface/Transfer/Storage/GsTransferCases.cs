using Curl.Conformance.Extended.GoogleCloudStorage;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Storage;

/// <summary>curl's generic transfer options (BL-4615) for <c>gs://</c> against the reference Cloud Storage XML API endpoint, anonymously; each check is <see cref="StorageTransferChecks" />'s.</summary>
[TestClass]
public sealed class GsTransferCases
{
    private static readonly StorageTransferTarget Target = new(
        "storage.googleapis.com",
        "gs://photos/hello.txt",
        "gs://photos/none.txt",
        "hello.txt",
        "hello, storage\n"u8.ToArray(),
        [],
        () =>
        {
            GcsReferenceServer server = new(new GcsAnonymousReferenceAuthorizer());
            server.PutObject("photos", "hello.txt", "hello, storage\n"u8.ToArray());
            return server;
        });

    [TestMethod]
    [SpecRequirement("CurlSurface:Verbose-Gs-1")]
    public Task Verbose_Download_TellsTheConnectionAndTlsOnStandardErrorOnly() => StorageTransferChecks.VerboseAsync(Target, "CurlSurface:Verbose-Gs-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:Output-Gs-1")]
    public Task Output_ToAFile_WritesTheObjectThereAndNothingElse() => StorageTransferChecks.OutputFileAsync(Target, "CurlSurface:Output-Gs-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:RemoteName-Gs-1")]
    public Task RemoteName_IntoAnOutputDirectory_WritesTheObjectUnderTheUrlsLastSegment() => StorageTransferChecks.RemoteNameAsync(Target, "CurlSurface:RemoteName-Gs-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:WriteOut-Gs-1")]
    public Task WriteOut_AfterADownload_ReportsEachVariable() => StorageTransferChecks.WriteOutAsync(Target, "CurlSurface:WriteOut-Gs-1", "gs");

    [TestMethod]
    [SpecRequirement("CurlSurface:Fail-Gs-1")]
    public Task Fail_MissingObject_Exits22WithNothingOnStandardOutput() => StorageTransferChecks.FailAsync(Target, "CurlSurface:Fail-Gs-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:FailWithBody-Gs-1")]
    public Task FailWithBody_MissingObject_Exits22AndWritesTheErrorBody() => StorageTransferChecks.FailWithBodyAsync(Target, "CurlSurface:FailWithBody-Gs-1", "NoSuchKey");

    [TestMethod]
    [SpecRequirement("CurlSurface:MaxTime-Gs-1")]
    public Task MaxTime_ServerNeverAnswers_Exits28AtTheDeadline() => StorageTransferChecks.MaxTimeAsync(Target, "CurlSurface:MaxTime-Gs-1");
}
