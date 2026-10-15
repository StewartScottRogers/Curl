using Curl.Api.Callbacks;
using Curl.Api.EngineContracts.Multi;

namespace Curl.Api.UnitTests.EngineContracts.Multi;

[TestClass]
public sealed class MultiConstantTests
{
    [TestMethod]
    [DataRow(CurlPoll.None, 0, DisplayName = "CURL_POLL_NONE")]
    [DataRow(CurlPoll.In, 1, DisplayName = "CURL_POLL_IN")]
    [DataRow(CurlPoll.Out, 2, DisplayName = "CURL_POLL_OUT")]
    [DataRow(CurlPoll.InOut, 3, DisplayName = "CURL_POLL_INOUT")]
    [DataRow(CurlPoll.Remove, 4, DisplayName = "CURL_POLL_REMOVE")]
    public void CurlPoll_Member_HasUpstreamNumber(CurlPoll poll, int expected)
    {
        Assert.AreEqual(expected, (int)poll);
    }

    [TestMethod]
    [DataRow(CurlSocketSelect.None, 0, DisplayName = "no event")]
    [DataRow(CurlSocketSelect.In, 0x01, DisplayName = "CURL_CSELECT_IN")]
    [DataRow(CurlSocketSelect.Out, 0x02, DisplayName = "CURL_CSELECT_OUT")]
    [DataRow(CurlSocketSelect.Error, 0x04, DisplayName = "CURL_CSELECT_ERR")]
    public void CurlSocketSelect_Member_HasUpstreamNumber(CurlSocketSelect select, int expected)
    {
        Assert.AreEqual(expected, (int)select);
    }

    [TestMethod]
    [DataRow(CurlPipelining.Nothing, 0, DisplayName = "CURLPIPE_NOTHING")]
    [DataRow(CurlPipelining.Http1, 1, DisplayName = "CURLPIPE_HTTP1")]
    [DataRow(CurlPipelining.Multiplex, 2, DisplayName = "CURLPIPE_MULTIPLEX")]
    public void CurlPipelining_Member_HasUpstreamNumber(CurlPipelining pipelining, int expected)
    {
        Assert.AreEqual(expected, (int)pipelining);
    }

    [TestMethod]
    [DataRow(CurlPushResult.Ok, 0, DisplayName = "CURL_PUSH_OK")]
    [DataRow(CurlPushResult.Deny, 1, DisplayName = "CURL_PUSH_DENY")]
    [DataRow(CurlPushResult.ErrorOut, 2, DisplayName = "CURL_PUSH_ERROROUT")]
    public void CurlPushResult_Member_HasUpstreamNumber(CurlPushResult result, int expected)
    {
        Assert.AreEqual(expected, (int)result);
    }

    [TestMethod]
    [DataRow(CurlMultiInfo.None, 0, DisplayName = "CURLMINFO_NONE")]
    [DataRow(CurlMultiInfo.TransfersCurrent, 1, DisplayName = "CURLMINFO_XFERS_CURRENT")]
    [DataRow(CurlMultiInfo.TransfersRunning, 2, DisplayName = "CURLMINFO_XFERS_RUNNING")]
    [DataRow(CurlMultiInfo.TransfersPending, 3, DisplayName = "CURLMINFO_XFERS_PENDING")]
    [DataRow(CurlMultiInfo.TransfersDone, 4, DisplayName = "CURLMINFO_XFERS_DONE")]
    [DataRow(CurlMultiInfo.TransfersAdded, 5, DisplayName = "CURLMINFO_XFERS_ADDED")]
    public void CurlMultiInfo_Member_HasUpstreamNumber(CurlMultiInfo info, int expected)
    {
        Assert.AreEqual(expected, (int)info);
    }

    [TestMethod]
    [DataRow(CurlMultiMessageType.None, 0, DisplayName = "CURLMSG_NONE")]
    [DataRow(CurlMultiMessageType.Done, 1, DisplayName = "CURLMSG_DONE")]
    public void CurlMultiMessageType_Member_HasUpstreamNumber(CurlMultiMessageType type, int expected)
    {
        Assert.AreEqual(expected, (int)type);
    }

    [TestMethod]
    [DataRow(CurlWaitEvents.None, 0, DisplayName = "no event")]
    [DataRow(CurlWaitEvents.PollIn, 0x0001, DisplayName = "CURL_WAIT_POLLIN")]
    [DataRow(CurlWaitEvents.PollPriority, 0x0002, DisplayName = "CURL_WAIT_POLLPRI")]
    [DataRow(CurlWaitEvents.PollOut, 0x0004, DisplayName = "CURL_WAIT_POLLOUT")]
    public void CurlWaitEvents_Member_HasUpstreamNumber(CurlWaitEvents events, int expected)
    {
        Assert.AreEqual(expected, (int)events);
    }

    [TestMethod]
    [DataRow(CurlMultiNotification.InfoRead, 0, DisplayName = "CURLMNOTIFY_INFO_READ")]
    [DataRow(CurlMultiNotification.EasyDone, 1, DisplayName = "CURLMNOTIFY_EASY_DONE")]
    public void CurlMultiNotification_Member_HasUpstreamNumber(CurlMultiNotification notification, int expected)
    {
        Assert.AreEqual(expected, (int)notification);
    }

    [TestMethod]
    [DataRow(CurlNetworkChange.None, 0, DisplayName = "nothing")]
    [DataRow(CurlNetworkChange.ClearConnections, 1, DisplayName = "CURLMNWC_CLEAR_CONNS")]
    [DataRow(CurlNetworkChange.ClearDns, 2, DisplayName = "CURLMNWC_CLEAR_DNS")]
    public void CurlNetworkChange_Member_HasUpstreamNumber(CurlNetworkChange change, int expected)
    {
        Assert.AreEqual(expected, (int)change);
    }

    [TestMethod]
    public void Enums_HaveExactlyUpstreamsMembers()
    {
        Assert.HasCount(5, Enum.GetValues<CurlPoll>());
        Assert.HasCount(4, Enum.GetValues<CurlSocketSelect>());
        Assert.HasCount(3, Enum.GetValues<CurlPipelining>());
        Assert.HasCount(3, Enum.GetValues<CurlPushResult>());
        Assert.HasCount(6, Enum.GetValues<CurlMultiInfo>());
        Assert.HasCount(2, Enum.GetValues<CurlMultiMessageType>());
        Assert.HasCount(4, Enum.GetValues<CurlWaitEvents>());
        Assert.HasCount(2, Enum.GetValues<CurlMultiNotification>());
        Assert.HasCount(3, Enum.GetValues<CurlNetworkChange>());
    }

    [TestMethod]
    public void SocketTimeout_IsCurlSocketBad()
    {
        Assert.AreEqual(CurlSocket.Bad, CurlMultiSockets.Timeout);
        Assert.AreEqual(-1, (long)CurlMultiSockets.Timeout.Handle);
    }
}
