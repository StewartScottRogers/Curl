using Curl.Api.Info;
using Curl.Api.Options;

namespace Libcurl.Info;

/// <summary>
/// The string, list and TLS pointer group's rows of <see cref="EasyInfoReaders"/>: each reads its
/// <c>CURLINFO_*</c> from the handle's <see cref="TransferInfo"/>, but <see cref="CurlInfo.Private"/>,
/// which reads <c>CURLOPT_PRIVATE</c> back from the options. Each reader is a named method in an explicit delegate, not a
/// lambda or a cached method group, so the coverage report adds no complexity or branch to this method for them.
/// </summary>
internal static partial class EasyInfoReaders
{
    static partial void AddStringReaders(Dictionary<CurlInfo, EasyInfoReader> readers)
    {
        readers[CurlInfo.EffectiveUrl] = new EasyInfoReader(ReadEffectiveUrl);
        readers[CurlInfo.EffectiveMethod] = new EasyInfoReader(ReadEffectiveMethod);
        readers[CurlInfo.RedirectUrl] = new EasyInfoReader(ReadRedirectUrl);
        readers[CurlInfo.ContentType] = new EasyInfoReader(ReadContentType);
        readers[CurlInfo.PrimaryIp] = new EasyInfoReader(ReadPrimaryIp);
        readers[CurlInfo.LocalIp] = new EasyInfoReader(ReadLocalIp);
        readers[CurlInfo.FtpEntryPath] = new EasyInfoReader(ReadFtpEntryPath);
        readers[CurlInfo.RtspSessionId] = new EasyInfoReader(ReadRtspSessionId);
        readers[CurlInfo.Scheme] = new EasyInfoReader(ReadScheme);
        readers[CurlInfo.Referer] = new EasyInfoReader(ReadReferer);
        readers[CurlInfo.Cainfo] = new EasyInfoReader(ReadCaInfo);
        readers[CurlInfo.Capath] = new EasyInfoReader(ReadCaPath);
        readers[CurlInfo.Private] = new EasyInfoReader(ReadPrivate);
        readers[CurlInfo.SslEngines] = new EasyInfoReader(ReadSslEngines);
        readers[CurlInfo.Cookielist] = new EasyInfoReader(ReadCookieList);
        readers[CurlInfo.Certinfo] = new EasyInfoReader(ReadCertInfo);
        readers[CurlInfo.TlsSslPtr] = new EasyInfoReader(ReadTlsSessionInfo);
        readers[CurlInfo.TlsSession] = new EasyInfoReader(ReadTlsSessionInfo);
    }

    private static object? ReadEffectiveUrl(TransferInfo info, EasyOptions options) => info.EffectiveUrl;

    private static object? ReadEffectiveMethod(TransferInfo info, EasyOptions options) => info.EffectiveMethod;

    private static object? ReadRedirectUrl(TransferInfo info, EasyOptions options) => info.RedirectUrl;

    private static object? ReadContentType(TransferInfo info, EasyOptions options) => info.ContentType;

    private static object? ReadPrimaryIp(TransferInfo info, EasyOptions options) => info.PrimaryIp;

    private static object? ReadLocalIp(TransferInfo info, EasyOptions options) => info.LocalIp;

    private static object? ReadFtpEntryPath(TransferInfo info, EasyOptions options) => info.FtpEntryPath;

    private static object? ReadRtspSessionId(TransferInfo info, EasyOptions options) => info.RtspSessionId;

    private static object? ReadScheme(TransferInfo info, EasyOptions options) => info.Scheme;

    private static object? ReadReferer(TransferInfo info, EasyOptions options) => info.Referer;

    private static object? ReadCaInfo(TransferInfo info, EasyOptions options) => info.CaInfo;

    private static object? ReadCaPath(TransferInfo info, EasyOptions options) => info.CaPath;

    private static object? ReadPrivate(TransferInfo info, EasyOptions options) => options.Behaviour.Private;

    private static object? ReadSslEngines(TransferInfo info, EasyOptions options) => info.SslEngines;

    private static object? ReadCookieList(TransferInfo info, EasyOptions options) => info.CookieList;

    private static object? ReadCertInfo(TransferInfo info, EasyOptions options) => info.CertInfo;

    private static object? ReadTlsSessionInfo(TransferInfo info, EasyOptions options) =>
        new CurlTlsSessionInfo(info.TlsSslBackend, null);
}
