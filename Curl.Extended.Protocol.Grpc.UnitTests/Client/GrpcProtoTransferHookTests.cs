using Curl.Extension.Abstractions.Transfer;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Grpc.Client;

/// <summary>Checks that <c>--grpc-proto</c> reaches a gRPC transfer and leaves every other transfer as it was.</summary>
[TestClass]
public sealed class GrpcProtoTransferHookTests
{
    [TestMethod]
    [DataRow("grpc://h/p.S/M")]
    [DataRow("grpcs://h/p.S/M")]
    public async Task ExecuteAsync_GrpcTransfer_GivesTheProtoFileToTheHandler(string url)
    {
        ITransferHook hook = new GrpcProtoTransferHook("svc.proto");
        ITransferContext? seen = null;

        _ = await hook.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream() }, context => { seen = context; return ValueTask.FromResult(TransferResult.Success(0)); });

        Assert.AreEqual("svc.proto", ((GrpcProtoContext)seen!).ProtoPath);
    }

    [TestMethod]
    public async Task ExecuteAsync_OtherTransfer_PassesItThroughUnchanged()
    {
        ITransferHook hook = new GrpcProtoTransferHook("svc.proto");
        var original = new TransferContext { Url = CurlUrl.Parse("http://h/"), Output = new MemoryStream() };
        ITransferContext? seen = null;

        _ = await hook.ExecuteAsync(original, context => { seen = context; return ValueTask.FromResult(TransferResult.Success(0)); });

        Assert.AreSame(original, seen);
    }
}
