using System.Text;

namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // Upstream's httpsig cases rely on the Debug feature, which lets a test fix the signing time at
    // created=0 (CURL_FORCETIME with CURL_HTTPSIG_CREATED). The harness stands in for it: it reports Debug to a
    // case that also names httpsig, and RunCurlAsync signs those commands with a clock fixed at the epoch (BL-2183).
    // The HSTS cases that fix the clock with CURL_TIME (446, 780-783) rely on it too: the harness reports Debug to a
    // case naming the HSTS feature and setting CURL_TIME, and RunCurlAsync has Curl honour CURL_TIME as the HSTS clock
    // (ADR-0493, BL-2234), and CURL_HSTS_HTTP, when set, as learning HSTS over plain http (ADR-0494, BL-2250).
    // The HSTS libtest case 1922 sets CURL_HSTS_HTTP without CURL_TIME: the harness reports Debug to a case naming HSTS
    // and setting it too, and CreateEngine's environment reader has the libtest engine's HSTS cache learn over http (BL-2676).
    // The Alt-Svc cases that set CURL_ALTSVC_HTTP (358, 359) rely on it to learn and look up Alt-Svc over plain http:
    // the harness reports Debug to a case naming the alt-svc feature and setting CURL_ALTSVC_HTTP, and RunCurlAsync has
    // Curl honour it (ADR-0497, BL-2263). The cases against nghttpx's TLS front on %HTTP2TLSPORT (2400, 2401, 2406) name
    // Debug only because upstream runs its http/2 cases on debug builds; they use nothing debug-only, so the harness
    // reports Debug to a case whose command names %HTTP2TLSPORT. Every other case sees no Debug, so the other Debug cases stay skipped.
    // A case naming the dns server (2102 to 2104) relies on override-dns, the debug build's CURL_DNS_SERVER: the harness
    // reports it to that case alone, and RunCurlAsync has Curl ask the dnsd stand-in on %DNSPORT through the datagram connector (ADR-0492, BL-2232).
    // The AWS SigV4 libtest cases (1933 to 1938) rely on Debug for CURL_FORCETIME, which signs at the epoch: the harness
    // reports Debug to a case naming the aws feature, and CreateEngine gives the libtest engine a signing clock fixed at
    // the epoch (ADR-0671, BL-2675); the curl tool's aws cases (439, 472, 1976, 3222, 5026, 5027) get Debug too, and
    // RunCurlAsync signs an --aws-sigv4 command at the epoch. The WebSocket libtest cases (2301 to 2304) rely on Debug for
    // CURL_ENTROPY, which pins Sec-WebSocket-Key and the frame masks: the harness reports Debug to a case naming the ws
    // feature and setting CURL_ENTROPY, and CreateEngine gives the libtest engine the forced sequence (ADR-0678, BL-2688);
    // cases 2700 to 2723 rely on it for CURL_WS_FORCE_ZERO_MASK, which zeroes the frame masks: the harness reports Debug
    // to a case naming ws and setting it, and CreateEngine gives the libtest engine zero masks (ADR-0698, BL-2698).
    // The FTP libtest case 753 relies on Debug for CURL_FTP_PWD_STOP, which stops the first transfer before PWD: the
    // harness reports Debug to a case setting it, and CreateEngine has the libtest engine stop its first perform there (ADR-0687, BL-2636).
    // The table below is the one shared edit a case needing a Debug stand-in makes: one line per rule, each telling
    // from the test file's text whether the case gets the Debug feature.
    private static readonly Func<string, bool>[] DebugStandInRules =
    [
        text => text.Contains("\nhttpsig\n", StringComparison.Ordinal),
        text => text.Contains("\naws\n", StringComparison.Ordinal),
        text => text.Contains("\nws\n", StringComparison.Ordinal)
            && (text.Contains("\nCURL_ENTROPY=", StringComparison.Ordinal) || text.Contains("\nCURL_WS_FORCE_ZERO_MASK=", StringComparison.Ordinal)),
        text => text.Contains("\nHSTS\n", StringComparison.Ordinal)
            && (text.Contains("\nCURL_TIME=", StringComparison.Ordinal) || text.Contains("\nCURL_HSTS_HTTP=", StringComparison.Ordinal)),
        text => text.Contains("\nalt-svc\n", StringComparison.Ordinal) && text.Contains("\nCURL_ALTSVC_HTTP=", StringComparison.Ordinal),
        text => text.Contains(":%HTTP2TLSPORT/", StringComparison.Ordinal),
        text => text.Contains("\nCURL_FTP_PWD_STOP=", StringComparison.Ordinal),
        text => text.Contains("\n" + FakeExtendedAttributesVariableName + "=", StringComparison.Ordinal),
    ];

    internal static UpstreamCurlPlatform PlatformFor(byte[] testFile)
    {
        string text = Encoding.Latin1.GetString(testFile);
        UpstreamCurlPlatform platform = DebugStandInRules.Any(rule => rule(text)) ? Platform.WithFeatures(["Debug"]) : Platform;
        return text.Contains("\n<server>\n", StringComparison.Ordinal) && text.Contains("\ndns\n", StringComparison.Ordinal)
            ? platform.WithFeatures(["override-dns"])
            : platform;
    }
}
