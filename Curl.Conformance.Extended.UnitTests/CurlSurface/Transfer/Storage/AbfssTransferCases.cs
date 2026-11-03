using Curl.Conformance.Extended.AzureStorage;
using Curl.Conformance.Extended.AzureStorage.DataLake;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Storage;

/// <summary>curl's generic transfer options (BL-4615) for <c>abfss://</c> against the reference DFS endpoint, with a bearer token and a bare account name; each check is <see cref="StorageTransferChecks" />'s.</summary>
[TestClass]
public sealed class AbfssTransferCases
{
    private static readonly StorageTransferTarget Target = new(
        "acct.dfs.core.windows.net",
        "abfss://fs@acct/hello.txt",
        "abfss://fs@acct/none.txt",
        "hello.txt",
        "hello, storage\n"u8.ToArray(),
        ["--oauth2-bearer", "tok"],
        () =>
        {
            AzureDataLakeReferenceEndpoint endpoint = new(AzureStorageResponseFraming.Chunked);
            endpoint.CreateFileSystem("fs");
            endpoint.StoreFile("fs", "hello.txt", "hello, storage\n"u8.ToArray());
            return endpoint;
        });

    [TestMethod]
    [SpecRequirement("CurlSurface:Verbose-Abfss-1")]
    public Task Verbose_Download_TellsTheConnectionAndTlsOnStandardErrorOnly() => StorageTransferChecks.VerboseAsync(Target, "CurlSurface:Verbose-Abfss-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:Output-Abfss-1")]
    public Task Output_ToAFile_WritesTheObjectThereAndNothingElse() => StorageTransferChecks.OutputFileAsync(Target, "CurlSurface:Output-Abfss-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:RemoteName-Abfss-1")]
    public Task RemoteName_IntoAnOutputDirectory_WritesTheObjectUnderTheUrlsLastSegment() => StorageTransferChecks.RemoteNameAsync(Target, "CurlSurface:RemoteName-Abfss-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:WriteOut-Abfss-1")]
    public Task WriteOut_AfterADownload_ReportsEachVariable() => StorageTransferChecks.WriteOutAsync(Target, "CurlSurface:WriteOut-Abfss-1", "abfss");

    [TestMethod]
    [SpecRequirement("CurlSurface:Fail-Abfss-1")]
    public Task Fail_MissingObject_Exits22WithNothingOnStandardOutput() => StorageTransferChecks.FailAsync(Target, "CurlSurface:Fail-Abfss-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:FailWithBody-Abfss-1")]
    public Task FailWithBody_MissingObject_Exits22AndWritesTheErrorBody() => StorageTransferChecks.FailWithBodyAsync(Target, "CurlSurface:FailWithBody-Abfss-1", "PathNotFound");

    [TestMethod]
    [SpecRequirement("CurlSurface:MaxTime-Abfss-1")]
    public Task MaxTime_ServerNeverAnswers_Exits28AtTheDeadline() => StorageTransferChecks.MaxTimeAsync(Target, "CurlSurface:MaxTime-Abfss-1");
}
