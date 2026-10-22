using Curl.Api.Info;
using Curl.Api.Options;

namespace Libcurl.Info;

/// <summary>
/// The long and socket group's rows of <see cref="EasyInfoReaders"/>: every <c>_LONG</c> info, the
/// <c>_SOCKET</c> <see cref="CurlInfo.Activesocket"/>, and the <c>_OFF_T</c> counts that are neither a
/// time nor a size (<see cref="CurlInfo.FiletimeT"/>, <see cref="CurlInfo.XferId"/>,
/// <see cref="CurlInfo.ConnId"/>). Each answers its <see cref="TransferInfo"/> property as the
/// <see cref="long"/> it is; <see cref="CurlInfo.Filetime"/> and <see cref="CurlInfo.FiletimeT"/> read
/// the same seconds, as <c>getinfo.c</c> answers both from one value. Each reader is a named method in
/// an explicit delegate, not a lambda or a cached method group, so the coverage report adds no
/// complexity or branch to this method for them.
/// </summary>
internal static partial class EasyInfoReaders
{
    static partial void AddLongReaders(Dictionary<CurlInfo, EasyInfoReader> readers)
    {
        readers[CurlInfo.ResponseCode] = new EasyInfoReader(ReadResponseCode);
        readers[CurlInfo.HttpConnectcode] = new EasyInfoReader(ReadHttpConnectCode);
        readers[CurlInfo.HttpVersion] = new EasyInfoReader(ReadHttpVersion);
        readers[CurlInfo.Filetime] = new EasyInfoReader(ReadFileTime);
        readers[CurlInfo.FiletimeT] = new EasyInfoReader(ReadFileTime);
        readers[CurlInfo.RedirectCount] = new EasyInfoReader(ReadRedirectCount);
        readers[CurlInfo.HeaderSize] = new EasyInfoReader(ReadHeaderSize);
        readers[CurlInfo.RequestSize] = new EasyInfoReader(ReadRequestSize);
        readers[CurlInfo.SslVerifyresult] = new EasyInfoReader(ReadSslVerifyResult);
        readers[CurlInfo.ProxySslVerifyresult] = new EasyInfoReader(ReadProxySslVerifyResult);
        readers[CurlInfo.HttpauthAvail] = new EasyInfoReader(ReadHttpAuthAvailable);
        readers[CurlInfo.ProxyauthAvail] = new EasyInfoReader(ReadProxyAuthAvailable);
        readers[CurlInfo.HttpauthUsed] = new EasyInfoReader(ReadHttpAuthUsed);
        readers[CurlInfo.ProxyauthUsed] = new EasyInfoReader(ReadProxyAuthUsed);
        readers[CurlInfo.OsErrno] = new EasyInfoReader(ReadOsErrno);
        readers[CurlInfo.NumConnects] = new EasyInfoReader(ReadNumConnects);
        readers[CurlInfo.PrimaryPort] = new EasyInfoReader(ReadPrimaryPort);
        readers[CurlInfo.LocalPort] = new EasyInfoReader(ReadLocalPort);
        readers[CurlInfo.ConditionUnmet] = new EasyInfoReader(ReadConditionUnmet);
        readers[CurlInfo.RtspClientCseq] = new EasyInfoReader(ReadRtspClientCSeq);
        readers[CurlInfo.RtspServerCseq] = new EasyInfoReader(ReadRtspServerCSeq);
        readers[CurlInfo.RtspCseqRecv] = new EasyInfoReader(ReadRtspCSeqReceived);
        readers[CurlInfo.Protocol] = new EasyInfoReader(ReadProtocol);
        readers[CurlInfo.ProxyError] = new EasyInfoReader(ReadProxyError);
        readers[CurlInfo.UsedProxy] = new EasyInfoReader(ReadUsedProxy);
        readers[CurlInfo.XferId] = new EasyInfoReader(ReadTransferId);
        readers[CurlInfo.ConnId] = new EasyInfoReader(ReadConnectionId);
        readers[CurlInfo.Lastsocket] = new EasyInfoReader(ReadLastSocket);
        readers[CurlInfo.Activesocket] = new EasyInfoReader(ReadActiveSocket);
    }

    private static object? ReadResponseCode(TransferInfo info, EasyOptions options) => info.ResponseCode;

    private static object? ReadHttpConnectCode(TransferInfo info, EasyOptions options) => info.HttpConnectCode;

    private static object? ReadHttpVersion(TransferInfo info, EasyOptions options) => info.HttpVersion;

    private static object? ReadFileTime(TransferInfo info, EasyOptions options) => info.FileTime;

    private static object? ReadRedirectCount(TransferInfo info, EasyOptions options) => info.RedirectCount;

    private static object? ReadHeaderSize(TransferInfo info, EasyOptions options) => info.HeaderSize;

    private static object? ReadRequestSize(TransferInfo info, EasyOptions options) => info.RequestSize;

    private static object? ReadSslVerifyResult(TransferInfo info, EasyOptions options) => info.SslVerifyResult;

    private static object? ReadProxySslVerifyResult(TransferInfo info, EasyOptions options) =>
        info.ProxySslVerifyResult;

    private static object? ReadHttpAuthAvailable(TransferInfo info, EasyOptions options) => info.HttpAuthAvailable;

    private static object? ReadProxyAuthAvailable(TransferInfo info, EasyOptions options) => info.ProxyAuthAvailable;

    private static object? ReadHttpAuthUsed(TransferInfo info, EasyOptions options) => info.HttpAuthUsed;

    private static object? ReadProxyAuthUsed(TransferInfo info, EasyOptions options) => info.ProxyAuthUsed;

    private static object? ReadOsErrno(TransferInfo info, EasyOptions options) => info.OsErrno;

    private static object? ReadNumConnects(TransferInfo info, EasyOptions options) => info.NumConnects;

    private static object? ReadPrimaryPort(TransferInfo info, EasyOptions options) => info.PrimaryPort;

    private static object? ReadLocalPort(TransferInfo info, EasyOptions options) => info.LocalPort;

    private static object? ReadConditionUnmet(TransferInfo info, EasyOptions options) => info.ConditionUnmet;

    private static object? ReadRtspClientCSeq(TransferInfo info, EasyOptions options) => info.RtspClientCSeq;

    private static object? ReadRtspServerCSeq(TransferInfo info, EasyOptions options) => info.RtspServerCSeq;

    private static object? ReadRtspCSeqReceived(TransferInfo info, EasyOptions options) => info.RtspCSeqReceived;

    /// <summary>
    /// <c>CURLPROTO_MASK</c> (<c>urldata.h</c>): the public <c>CURLPROTO_*</c> bits, HTTP (bit 0) up to GOPHERS
    /// (bit 29). <c>url.c</c> masks the connection's protocol with it for <see cref="CurlInfo.Protocol"/>, so a
    /// ws, wss or mqtts transfer (bits 30, 31 and 32) reports 0.
    /// </summary>
    internal const long ProtocolMask = 0x3fffffff;

    private static object? ReadProtocol(TransferInfo info, EasyOptions options) => info.Protocol & ProtocolMask;

    private static object? ReadProxyError(TransferInfo info, EasyOptions options) => info.ProxyError;

    private static object? ReadUsedProxy(TransferInfo info, EasyOptions options) => info.UsedProxy;

    private static object? ReadTransferId(TransferInfo info, EasyOptions options) => info.TransferId;

    private static object? ReadConnectionId(TransferInfo info, EasyOptions options) => info.ConnectionId;

    private static object? ReadLastSocket(TransferInfo info, EasyOptions options) => info.LastSocket;

    private static object? ReadActiveSocket(TransferInfo info, EasyOptions options) => info.ActiveSocket;
}
