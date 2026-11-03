using Curl.Conformance.Extended.AzureStorage;
using Curl.Conformance.Extended.AzureStorage.DataLake;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Storage;

/// <summary>curl's generic transfer options (BL-4615) for <c>abfs://</c> against the reference DFS endpoint, with a bearer token and the full host; each check is <see cref="StorageTransferChecks" />'s.</summary>
[TestClass]
public sealed class AbfsTransferCases
{
    private static readonly StorageTransferTarget Target = new(
        "acct.dfs.core.windows.net",
        "abfs://fs@acct.dfs.core.windows.net/hello.txt",
        "abfs://fs@acct.dfs.core.windows.net/none.txt",
        "hello.txt",
        "hello, storage\n"u8.ToArray(),
        ["--oauth2-bearer", "tok"],
        () =>
        {
            AzureDataLakeReferenceEndpoint endpoint = new(AzureStorageResponseFraming.ContentLength);
            endpoint.CreateFileSystem("fs");
            endpoint.StoreFile("fs", "hello.txt", "hello, storage\n"u8.ToArray());
            return endpoint;
        });

    [TestMethod]
    [SpecRequirement("CurlSurface:Verbose-Abfs-1")]
    public Task Verbose_Download_TellsTheConnectionAndTlsOnStandardErrorOnly() => StorageTransferChecks.VerboseAsync(Target, "CurlSurface:Verbose-Abfs-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:Output-Abfs-1")]
    public Task Output_ToAFile_WritesTheObjectThereAndNothingElse() => StorageTransferChecks.OutputFileAsync(Target, "CurlSurface:Output-Abfs-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:RemoteName-Abfs-1")]
    public Task RemoteName_IntoAnOutputDirectory_WritesTheObjectUnderTheUrlsLastSegment() => StorageTransferChecks.RemoteNameAsync(Target, "CurlSurface:RemoteName-Abfs-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:WriteOut-Abfs-1")]
    public Task WriteOut_AfterADownload_ReportsEachVariable() => StorageTransferChecks.WriteOutAsync(Target, "CurlSurface:WriteOut-Abfs-1", "abfs");

    [TestMethod]
    [SpecRequirement("CurlSurface:Fail-Abfs-1")]
    public Task Fail_MissingObject_Exits22WithNothingOnStandardOutput() => StorageTransferChecks.FailAsync(Target, "CurlSurface:Fail-Abfs-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:FailWithBody-Abfs-1")]
    public Task FailWithBody_MissingObject_Exits22AndWritesTheErrorBody() => StorageTransferChecks.FailWithBodyAsync(Target, "CurlSurface:FailWithBody-Abfs-1", "PathNotFound");

    [TestMethod]
    [SpecRequirement("CurlSurface:MaxTime-Abfs-1")]
    public Task MaxTime_ServerNeverAnswers_Exits28AtTheDeadline() => StorageTransferChecks.MaxTimeAsync(Target, "CurlSurface:MaxTime-Abfs-1");
}
