using Curl.Api.Codes;
using Curl.Api.Info;
using Libcurl.Easy;
using Libcurl.Info;
using Libcurl.Options;
using Libcurl.UnitTests.Easy;

namespace Libcurl.UnitTests.Info.Longs;

[TestClass]
public sealed class EasyInfoReadersLongsTests
{
    // Every value is distinct, so a reader that reads the wrong property fails.
    private static readonly TransferInfo Reported = new()
    {
        ResponseCode = 201,
        HttpConnectCode = 202,
        HttpVersion = 3,
        FileTime = 1_700_000_000,
        RedirectCount = 4,
        HeaderSize = 205,
        RequestSize = 206,
        SslVerifyResult = 207,
        ProxySslVerifyResult = 208,
        HttpAuthAvailable = 209,
        ProxyAuthAvailable = 210,
        HttpAuthUsed = 211,
        ProxyAuthUsed = 212,
        OsErrno = 213,
        NumConnects = 214,
        PrimaryPort = 215,
        LocalPort = 216,
        ConditionUnmet = 1,
        RtspClientCSeq = 218,
        RtspServerCSeq = 219,
        RtspCSeqReceived = 220,
        Protocol = 221,
        ProxyError = 222,
        UsedProxy = 5,
        TransferId = 224,
        ConnectionId = 225,
        LastSocket = 226,
        ActiveSocket = 227,
    };

    [TestMethod]
    [DataRow(CurlInfo.ResponseCode, 201L)]
    [DataRow(CurlInfo.HttpConnectcode, 202L)]
    [DataRow(CurlInfo.HttpVersion, 3L)]
    [DataRow(CurlInfo.Filetime, 1_700_000_000L)]
    [DataRow(CurlInfo.FiletimeT, 1_700_000_000L)]
    [DataRow(CurlInfo.RedirectCount, 4L)]
    [DataRow(CurlInfo.HeaderSize, 205L)]
    [DataRow(CurlInfo.RequestSize, 206L)]
    [DataRow(CurlInfo.SslVerifyresult, 207L)]
    [DataRow(CurlInfo.ProxySslVerifyresult, 208L)]
    [DataRow(CurlInfo.HttpauthAvail, 209L)]
    [DataRow(CurlInfo.ProxyauthAvail, 210L)]
    [DataRow(CurlInfo.HttpauthUsed, 211L)]
    [DataRow(CurlInfo.ProxyauthUsed, 212L)]
    [DataRow(CurlInfo.OsErrno, 213L)]
    [DataRow(CurlInfo.NumConnects, 214L)]
    [DataRow(CurlInfo.PrimaryPort, 215L)]
    [DataRow(CurlInfo.LocalPort, 216L)]
    [DataRow(CurlInfo.ConditionUnmet, 1L)]
    [DataRow(CurlInfo.RtspClientCseq, 218L)]
    [DataRow(CurlInfo.RtspServerCseq, 219L)]
    [DataRow(CurlInfo.RtspCseqRecv, 220L)]
    [DataRow(CurlInfo.Protocol, 221L)]
    [DataRow(CurlInfo.ProxyError, 222L)]
    [DataRow(CurlInfo.UsedProxy, 5L)]
    [DataRow(CurlInfo.XferId, 224L)]
    [DataRow(CurlInfo.ConnId, 225L)]
    [DataRow(CurlInfo.Lastsocket, 226L)]
    [DataRow(CurlInfo.Activesocket, 227L)]
    public void GetInfo_ALongInfo_IsTheReportedLong(CurlInfo info, long expected)
    {
        Assert.AreEqual(expected, Read(Reported, info));
    }

    [TestMethod]
    [DataRow(CurlInfo.ResponseCode, 0L)]
    [DataRow(CurlInfo.Filetime, -1L)]
    [DataRow(CurlInfo.FiletimeT, -1L)]
    [DataRow(CurlInfo.XferId, -1L)]
    [DataRow(CurlInfo.ConnId, -1L)]
    [DataRow(CurlInfo.Lastsocket, -1L)]
    [DataRow(CurlInfo.Activesocket, -1L)]
    public void GetInfo_ALongInfoOnAFreshHandle_IsLibcurlsDefault(CurlInfo info, long expected)
    {
        Assert.AreEqual(expected, Read(new TransferInfo(), info));
    }

    [TestMethod]
    public void GetInfo_PrimaryPortOfATransferThatMadeNoConnection_IsMinusOne()
    {
        Assert.AreEqual(-1L, Read(new TransferInfo(), CurlInfo.PrimaryPort));
    }

    [TestMethod]
    [DataRow(1L << 30)]
    [DataRow(1L << 31)]
    [DataRow(1L << 32)]
    public void GetInfo_ProtocolOfAWsWssOrMqttsTransfer_IsZeroAsCurlProtoMaskLeavesIt(long protocol)
    {
        Assert.AreEqual(0L, Read(new TransferInfo { Protocol = protocol }, CurlInfo.Protocol));
    }

    private static object? Read(TransferInfo reported, CurlInfo info)
    {
        using var easy = new CurlEasy(new FakeEasyTransfer { Info = reported }, EasyOptionDispatch.Default);

        var code = easy.GetInfo(info, out var value);

        Assert.AreEqual(CurlCode.Ok, code);
        return value;
    }
}
