using System.Net;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.OsCredentialStore.UnitTests;

[TestClass]
public sealed class OsCredentialStoreTransferHookTests
{
    [TestMethod]
    public async Task ExecuteAsync_CredentialStored_RunsTheTransferWithItInPlaceOfUser()
    {
        NetworkCredential stored = new("alice", "s3cret");
        ITransferHook hook = new OsCredentialStoreTransferHook(new FixedStore(stored), "example.com");
        TransferContext context = new() { Url = CurlUrl.Parse("http://example.com/"), Output = new MemoryStream(), Credentials = new NetworkCredential("cli", "pw") };
        ITransferContext? sent = null;

        var result = await hook.ExecuteAsync(context, given =>
        {
            sent = given;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.IsTrue(result.IsSuccess);
        Assert.AreSame(stored, sent!.Credentials);
        Assert.AreSame(context.Url, sent.Url);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoCredentialStored_FailsWithExit67WithoutConnecting()
    {
        ITransferHook hook = new OsCredentialStoreTransferHook(new FixedStore(null), "missing");
        TransferContext context = new() { Url = CurlUrl.Parse("ftp://example.com/"), Output = new MemoryStream() };
        var ran = false;

        var result = await hook.ExecuteAsync(context, _ =>
        {
            ran = true;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.IsFalse(ran);
        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual("No credential named missing in the fixed store", result.ErrorMessage);
    }

    private sealed class FixedStore(NetworkCredential? credential) : ICredentialStore
    {
        public string Name => "fixed store";

        public ValueTask<NetworkCredential?> FindAsync(string name, CancellationToken cancellationToken) => ValueTask.FromResult(credential);
    }
}
