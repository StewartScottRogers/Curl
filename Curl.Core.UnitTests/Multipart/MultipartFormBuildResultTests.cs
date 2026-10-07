using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core.Multipart;

[TestClass]
public sealed class MultipartFormBuildResultTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void IsBuiltFollowsTheBody()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using MemoryStream content = new();
        StreamBody body = new(content, 0, "multipart/form-data; boundary=b");
        TransferResult failure = TransferResult.Failure(CurlExitCode.ReadError, "m");
        diagnostics.Arrange("failed result", "no body, failure ReadError");
        MultipartFormBuildResult failed = new(null, failure);

        MultipartFormBuildResult built = failed with { Body = body, Failure = null };

        diagnostics.Act("failed.IsBuilt", failed.IsBuilt);
        diagnostics.Act("built.IsBuilt", built.IsBuilt);
        diagnostics.Assert("failed.IsBuilt", false, failed.IsBuilt);
        Assert.IsFalse(failed.IsBuilt);
        diagnostics.Assert("built.IsBuilt", true, built.IsBuilt);
        Assert.IsTrue(built.IsBuilt);
        diagnostics.Assert("built.Body is the body", true, ReferenceEquals(body, built.Body));
        Assert.AreSame(body, built.Body);
        diagnostics.Assert("built.Failure", null, built.Failure);
        Assert.IsNull(built.Failure);
    }
}
