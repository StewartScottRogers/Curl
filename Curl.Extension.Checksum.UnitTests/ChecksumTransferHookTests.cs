using System.Text;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Checksum.UnitTests;

[TestClass]
public sealed class ChecksumTransferHookTests
{
    // FIPS 180-4 §6.2 (SHA-256), NIST's worked example for the one-block message "abc".
    private const string Sha256OfAbc = "sha256:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [TestMethod]
    public async Task ExecuteAsync_TheBodyHasTheExpectedDigest_GivesTheTransfersResultAndTheBodyUnchanged()
    {
        var output = new MemoryStream();
        var succeeded = new TransferResult(CurlExitCode.Ok, 3);

        var result = await HookExpecting(Sha256OfAbc).ExecuteAsync(ContextWriting(output), WritingBody("abc", succeeded));

        Assert.AreSame(succeeded, result);
        Assert.AreEqual("abc", Encoding.ASCII.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_TheBodyHasAnotherDigest_FailsWithExit61NamingBothDigests()
    {
        var output = new MemoryStream();

        var result = await HookExpecting(Sha256OfAbc).ExecuteAsync(ContextWriting(output), WritingBody("abd", new TransferResult(CurlExitCode.Ok, 3)));

        Assert.AreEqual(CurlExitCode.BadContentEncoding, result.ExitCode);
        Assert.AreEqual(3, result.BytesTransferred);
        Assert.AreEqual($"checksum mismatch: expected {Sha256OfAbc}, got sha256:a52d159f262b2c6ddb724a61840befc36eb30c88877a4030b65cbe86298449c9", result.ErrorMessage);
        Assert.AreEqual("abd", Encoding.ASCII.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_TheTransferFailed_GivesItsResultWithoutComparing()
    {
        var failed = TransferResult.Failure(CurlExitCode.CouldntConnect, "refused");

        var result = await HookExpecting(Sha256OfAbc).ExecuteAsync(ContextWriting(new MemoryStream()), WritingBody(string.Empty, failed));

        Assert.AreSame(failed, result);
    }

    [TestMethod]
    public async Task ExecuteAsync_TheBodyArrivesInPieces_HashesThemAllInOrder()
    {
        var context = ContextWriting(new MemoryStream());

        var result = await HookExpecting(Sha256OfAbc).ExecuteAsync(context, async inner =>
        {
            inner.Output.Write("a"u8);
            await inner.Output.WriteAsync("b"u8.ToArray());
            inner.Output.Write("c"u8.ToArray(), 0, 1);
            return new TransferResult(CurlExitCode.Ok, 3);
        });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_KeepsTheHeaderOutputs()
    {
        var headers = new MemoryStream();
        var dumped = new MemoryStream();
        var context = new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = Stream.Null, HeaderOutput = headers, DumpHeaderOutput = dumped };

        await HookExpecting(Sha256OfAbc).ExecuteAsync(context, inner =>
        {
            Assert.AreSame(headers, inner.HeaderOutput);
            Assert.AreSame(dumped, inner.DumpHeaderOutput);
            return ValueTask.FromResult(new TransferResult(CurlExitCode.Ok, 0));
        });
    }

    private static ChecksumTransferHook HookExpecting(string value) => new(ExpectedChecksum.TryParse(value)!);

    private static TransferContext ContextWriting(Stream output) => new() { Url = CurlUrl.Parse("http://example.com/"), Output = output };

    private static Func<ITransferContext, ValueTask<TransferResult>> WritingBody(string body, TransferResult result) => async context =>
    {
        await context.Output.WriteAsync(Encoding.ASCII.GetBytes(body));
        return result;
    };
}
