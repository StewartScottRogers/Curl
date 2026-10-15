using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Info;
using Curl.Api.Options;

namespace Curl.Api.UnitTests.EngineContracts.Multi;

[TestClass]
public sealed class MultiValueTypeTests
{
    [TestMethod]
    public void CurlMultiMessage_KeepsTypeTransferAndResult()
    {
        var transfer = new UnusedTransfer();

        var message = new CurlMultiMessage(CurlMultiMessageType.Done, transfer, CurlCode.CouldntResolveHost);

        Assert.AreEqual(CurlMultiMessageType.Done, message.Type);
        Assert.AreSame(transfer, message.Transfer);
        Assert.AreEqual(CurlCode.CouldntResolveHost, message.Result);
    }

    [TestMethod]
    public void CurlWaitSocket_KeepsSocketAndEvents()
    {
        var wait = new CurlWaitSocket(new CurlSocket(7), CurlWaitEvents.PollIn | CurlWaitEvents.PollOut, CurlWaitEvents.PollIn);

        Assert.AreEqual(new CurlSocket(7), wait.Socket);
        Assert.AreEqual(CurlWaitEvents.PollIn | CurlWaitEvents.PollOut, wait.Events);
        Assert.AreEqual(CurlWaitEvents.PollIn, wait.ReturnedEvents);
    }

    [TestMethod]
    public void CurlWaitSocket_Default_IsSocketZeroWithNoEvents()
    {
        var wait = default(CurlWaitSocket);

        Assert.AreEqual(new CurlSocket(0), wait.Socket);
        Assert.AreEqual(CurlWaitEvents.None, wait.Events);
        Assert.AreEqual(CurlWaitEvents.None, wait.ReturnedEvents);
    }

    private sealed class UnusedTransfer : IEasyTransfer
    {
        public EasyOptions Options { get; set; } = new();

        public TransferInfo Info => throw new NotSupportedException();

        public string ErrorText => "";

        public ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public IEasyTransfer Duplicate() => throw new NotSupportedException();

        public void Reset() => throw new NotSupportedException();

        public CurlCode Pause(CurlPause directions) => throw new NotSupportedException();

        public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
