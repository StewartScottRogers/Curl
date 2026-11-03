using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.OciRegistry;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Storage;

/// <summary>curl's generic transfer options (BL-4615) for <c>oci://</c> against the reference registry, pulling a manifest by tag; each check is <see cref="StorageTransferChecks" />'s.</summary>
[TestClass]
public sealed class OciTransferCases
{
    private static readonly StorageTransferTarget Target = new(
        "registry.example",
        "oci://registry.example/lib/app:1.0",
        "oci://registry.example/lib/app:none",
        "app:1.0",
        "{\"schemaVersion\":2}"u8.ToArray(),
        [],
        () =>
        {
            OciReferenceRegistry registry = new();
            registry.PutManifest("lib/app", "1.0", "application/vnd.oci.image.manifest.v1+json", "{\"schemaVersion\":2}"u8.ToArray());
            return registry;
        });

    [TestMethod]
    [SpecRequirement("CurlSurface:Verbose-Oci-1")]
    public Task Verbose_Download_TellsTheConnectionAndTlsOnStandardErrorOnly() => StorageTransferChecks.VerboseAsync(Target, "CurlSurface:Verbose-Oci-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:Output-Oci-1")]
    public Task Output_ToAFile_WritesTheObjectThereAndNothingElse() => StorageTransferChecks.OutputFileAsync(Target, "CurlSurface:Output-Oci-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:WriteOut-Oci-1")]
    public Task WriteOut_AfterADownload_ReportsEachVariable() => StorageTransferChecks.WriteOutAsync(Target, "CurlSurface:WriteOut-Oci-1", "oci");

    [TestMethod]
    [SpecRequirement("CurlSurface:Fail-Oci-1")]
    public Task Fail_MissingObject_Exits22WithNothingOnStandardOutput() => StorageTransferChecks.FailAsync(Target, "CurlSurface:Fail-Oci-1");

    [TestMethod]
    [SpecRequirement("CurlSurface:FailWithBody-Oci-1")]
    public Task FailWithBody_MissingObject_Exits22AndWritesTheErrorBody() => StorageTransferChecks.FailWithBodyAsync(Target, "CurlSurface:FailWithBody-Oci-1", "MANIFEST_UNKNOWN");

    [TestMethod]
    [SpecRequirement("CurlSurface:MaxTime-Oci-1")]
    public Task MaxTime_ServerNeverAnswers_Exits28AtTheDeadline() => StorageTransferChecks.MaxTimeAsync(Target, "CurlSurface:MaxTime-Oci-1");
}
