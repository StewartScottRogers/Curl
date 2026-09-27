using Curl.Protocol.Abstractions;

namespace Curl.Core.Multipart;

[TestClass]
public sealed class MultipartFormBuildResultTests
{
    [TestMethod]
    public void IsBuiltFollowsTheBody()
    {
        using MemoryStream content = new();
        StreamBody body = new(content, 0, "multipart/form-data; boundary=b");
        TransferResult failure = TransferResult.Failure(CurlExitCode.ReadError, "m");
        MultipartFormBuildResult failed = new(null, failure);

        MultipartFormBuildResult built = failed with { Body = body, Failure = null };

        Assert.IsFalse(failed.IsBuilt);
        Assert.IsTrue(built.IsBuilt);
        Assert.AreSame(body, built.Body);
        Assert.IsNull(built.Failure);
    }
}
