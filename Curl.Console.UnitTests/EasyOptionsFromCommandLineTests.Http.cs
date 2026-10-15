using System.Text;
using Curl.Api.Options.Http;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="EasyOptionsFromCommandLine.HttpOf" />: each HTTP command-line option given sets its
/// <see cref="EasyHttpOptions" /> member, and a command line without it keeps libcurl's default (ADR-0527).
/// </summary>
public sealed partial class EasyOptionsFromCommandLineTests
{
    private static readonly EasyHttpOptions LibcurlHttpDefaults = new();

    public static IEnumerable<object?[]> GivenHttpOptions =>
    [
        GivenHttp("-H", o => string.Join('|', o.HttpHeaders), "X-A: 1|X-B;", "-H", "X-A: 1", "-H", "X-B;"),
        GivenHttp("--json headers", o => string.Join('|', o.HttpHeaders), "Content-Type: application/json|Accept: application/json", "--json", "{}"),
        GivenHttp("--json after -H", o => string.Join('|', o.HttpHeaders), "content-type: text/plain|Accept: application/json", "-H", "content-type: text/plain", "--json", "{}"),
        GivenHttp("--json beside -H Accept", o => string.Join('|', o.HttpHeaders), "Accept: */*|Content-Type: application/json", "-H", "Accept: */*", "--json", "{}"),
        GivenHttp("-A", o => o.UserAgent, "agent/1", "-A", "agent/1"),
        GivenHttp("-A empty", o => o.UserAgent, "", "-A", ""),
        GivenHttp("-e", o => o.Referer, "https://from.example/", "-e", "https://from.example/"),
        GivenHttp("-e ;auto", o => o.AutoReferer, true, "-e", "https://from.example/;auto"),
        GivenHttp("-e ;auto referer", o => o.Referer, "https://from.example/", "-e", "https://from.example/;auto"),
        GivenHttp("--compressed", o => o.AcceptEncoding, "", "--compressed"),
        GivenHttp("--tr-encoding", o => o.TransferEncoding, true, "--tr-encoding"),
        GivenHttp("--raw content", o => o.HttpContentDecoding, false, "--raw"),
        GivenHttp("--raw transfer", o => o.HttpTransferDecoding, false, "--raw"),
        GivenHttp("--ignore-content-length", o => o.IgnoreContentLength, true, "--ignore-content-length"),
        GivenHttp("-L", o => o.FollowLocation, FollowLocation.All, "-L"),
        GivenHttp("--location-trusted", o => o.FollowLocation, FollowLocation.All, "--location-trusted"),
        GivenHttp("--follow", o => o.FollowLocation, FollowLocation.ObeyCode, "--follow"),
        GivenHttp("--follow then -L", o => o.FollowLocation, FollowLocation.All, "--follow", "-L"),
        GivenHttp("--no-location", o => o.FollowLocation, FollowLocation.None, "-L", "--no-location"),
        GivenHttp("--max-redirs", o => o.MaxRedirects, 7L, "--max-redirs", "7"),
        GivenHttp("--max-redirs -1", o => o.MaxRedirects, -1L, "--max-redirs", "-1"),
        GivenHttp("--post301", o => o.PostRedirect, PostRedirect.Post301, "--post301"),
        GivenHttp("--post302", o => o.PostRedirect, PostRedirect.Post302, "--post302"),
        GivenHttp("--post303", o => o.PostRedirect, PostRedirect.Post303, "--post303"),
        GivenHttp("--post301 --post302 --post303", o => o.PostRedirect, PostRedirect.All, "--post301", "--post302", "--post303"),
        GivenHttp("-d", o => BodyText(o), "a=1&b=2", "-d", "a=1", "-d", "b=2"),
        GivenHttp("-d size", o => o.PostFieldSize, 7L, "-d", "a=1", "-d", "b=2"),
        GivenHttp("-d empty", o => BodyText(o), "", "-d", ""),
        GivenHttp("-d empty size", o => o.PostFieldSize, 0L, "-d", ""),
        GivenHttp("--data-binary", o => BodyText(o), "x y", "--data-binary", "x y"),
        GivenHttp("--data-raw", o => BodyText(o), "@raw", "--data-raw", "@raw"),
        GivenHttp("--data-urlencode", o => BodyText(o), "a%26b", "--data-urlencode", "a&b"),
        GivenHttp("--json body", o => BodyText(o), "{\"a\":1}", "--json", "{\"a\":1}"),
        GivenHttp("-G drops the body", o => o.PostFields, null, "-G", "-d", "a=1"),
        GivenHttp("-G keeps the size default", o => o.PostFieldSize, LibcurlHttpDefaults.PostFieldSize, "-G", "-d", "a=1"),
        GivenHttp("--form-escape", o => o.MimeOptions, MimeOptions.FormEscape, "--form-escape"),
        GivenHttp("--form-escape with -d", o => o.MimeOptions, MimeOptions.FormEscape, "--form-escape", "-d", "a=1"),
        GivenHttp("--http1.0", o => o.HttpVersion, HttpVersion.Http10, "--http1.0"),
        GivenHttp("-0", o => o.HttpVersion, HttpVersion.Http10, "-0"),
        GivenHttp("--http1.1", o => o.HttpVersion, HttpVersion.Http11, "--http1.1"),
        GivenHttp("--http2", o => o.HttpVersion, HttpVersion.Http2, "--http2"),
        GivenHttp("--http2-prior-knowledge", o => o.HttpVersion, HttpVersion.Http2PriorKnowledge, "--http2-prior-knowledge"),
        GivenHttp("--http3", o => o.HttpVersion, HttpVersion.Http3, "--http3"),
        GivenHttp("--http3-only", o => o.HttpVersion, HttpVersion.Http3Only, "--http3-only"),
        GivenHttp("last version wins", o => o.HttpVersion, HttpVersion.Http11, "--http2", "--http1.1"),
        GivenHttp("--http0.9", o => o.Http09Allowed, true, "--http0.9"),
        GivenHttp("--expect100-timeout", o => o.Expect100Timeout, TimeSpan.FromMilliseconds(2500), "--expect100-timeout", "2.5"),
        GivenHttp("--expect100-timeout 0", o => o.Expect100Timeout, LibcurlHttpDefaults.Expect100Timeout, "--expect100-timeout", "0"),
        GivenHttp("--request-target", o => o.RequestTarget, "*", "--request-target", "*"),
        GivenHttp("-b string", o => o.Cookie, "a=1", "-b", "a=1"),
        GivenHttp("-b strings", o => o.Cookie, "a=1; b=2", "-b", "a=1", "-b", "b=2"),
        GivenHttp("-b string is no file", o => o.CookieFiles.Count, 0, "-b", "a=1"),
        GivenHttp("-b file", o => string.Join('|', o.CookieFiles), "jar.txt|other.txt", "-b", "jar.txt", "-b", "a=1", "-b", "other.txt"),
        GivenHttp("-b file is no string", o => o.Cookie, null, "-b", "jar.txt"),
        GivenHttp("-b empty", o => string.Join('|', o.CookieFiles), "", "-b", ""),
        GivenHttp("-b empty count", o => o.CookieFiles.Count, 1, "-b", ""),
        GivenHttp("-c", o => o.CookieJar, "jar.txt", "-c", "jar.txt"),
        GivenHttp("-j", o => o.CookieSession, true, "-j"),
        GivenHttp("--alt-svc", o => o.AltSvc, "altsvc.txt", "--alt-svc", "altsvc.txt"),
        GivenHttp("--alt-svc control", o => o.AltSvcControl, AltSvcControl.H1 | AltSvcControl.H2 | AltSvcControl.H3, "--alt-svc", "altsvc.txt"),
        GivenHttp("--alt-svc empty", o => o.AltSvc, "", "--alt-svc", ""),
        GivenHttp("--hsts", o => o.Hsts, "hsts.txt", "--hsts", "hsts.txt"),
        GivenHttp("--hsts control", o => o.HstsControl, HstsControl.Enable, "--hsts", "hsts.txt"),
        GivenHttp("--no-alpn", o => o.SslEnableAlpn, false, "--no-alpn"),
        GivenHttp("--alpn", o => o.SslEnableAlpn, true, "--no-alpn", "--alpn"),
        GivenHttp("-F text", MimeText, "[a|||||bytes:b]", "-F", "a=b"),
        GivenHttp("-F no name", MimeText, "[|||||bytes:v]", "-F", "=v"),
        GivenHttp("-F parts in order", MimeText, "[a|||||bytes:1] [b|||||bytes:2]", "-F", "a=1", "-F", "b=2"),
        GivenHttp("--form-string", MimeText, "[a|||||bytes:@x;type=y]", "--form-string", "a=@x;type=y"),
        GivenHttp("-F @file", MimeText, "[f|up.txt||||file:dir/up.txt]", "-F", "f=@dir/up.txt"),
        GivenHttp("-F @file backslash", MimeText, "[f|up.txt||||file:dir\\up.txt]", "-F", "f=@dir\\up.txt"),
        GivenHttp("-F <file", MimeText, "[c|||||file:dir/in.txt]", "-F", "c=<dir/in.txt"),
        GivenHttp("-F @-", MimeText, "[u|-||||stdin]", "-F", "u=@-"),
        GivenHttp("-F <-", MimeText, "[c|||||stdin]", "-F", "c=<-"),
        GivenHttp("-F ;type=", MimeText, "[a||text/x|||bytes:b]", "-F", "a=b;type=text/x"),
        GivenHttp("-F ;filename=", MimeText, "[f|other.txt||||file:up.txt]", "-F", "f=@up.txt;filename=other.txt"),
        GivenHttp("-F text ;filename=", MimeText, "[a|n.txt||||bytes:b]", "-F", "a=b;filename=n.txt"),
        GivenHttp("-F ;headers=", MimeText, "[a||||X-A: 1,X-B: 2|bytes:b]", "-F", "a=b;headers=X-A: 1;headers=X-B: 2"),
        GivenHttp("-F ;encoder=", MimeText, "[a|||base64||bytes:b]", "-F", "a=b;encoder=base64"),
        GivenHttp("-F multipart", MimeText, "[m||multipart/mixed|||none|([x|||||bytes:1] [y|t.txt||||file:t.txt])]", "-F", "m=(;type=multipart/mixed", "-F", "x=1", "-F", "y=@t.txt", "-F", "=)"),
        GivenHttp("-F empty multipart", MimeText, "[m|||||none|()]", "-F", "m=(", "-F", "=)"),
        GivenHttp("-F @file list", MimeText, "[g|||||none|([|a.txt||||file:a.txt] [|b.txt||||file:b.txt])]", "-F", "g=@a.txt,b.txt"),
        GivenHttp("-F with --form-escape", o => o.MimePost?.Parts.Count, 1, "--form-escape", "-F", "a=b"),
    ];

    public static IEnumerable<object?[]> AbsentHttpOptions =>
    [
        AbsentHttp("HttpHeaders", o => o.HttpHeaders.Count),
        AbsentHttp("UserAgent", o => o.UserAgent),
        AbsentHttp("Referer", o => o.Referer),
        AbsentHttp("AutoReferer", o => o.AutoReferer),
        AbsentHttp("AcceptEncoding", o => o.AcceptEncoding),
        AbsentHttp("TransferEncoding", o => o.TransferEncoding),
        AbsentHttp("HttpContentDecoding", o => o.HttpContentDecoding),
        AbsentHttp("HttpTransferDecoding", o => o.HttpTransferDecoding),
        AbsentHttp("FollowLocation", o => o.FollowLocation),
        AbsentHttp("PostRedirect", o => o.PostRedirect),
        AbsentHttp("Post", o => o.Post),
        AbsentHttp("PostFields", o => o.PostFields),
        AbsentHttp("PostFieldSize", o => o.PostFieldSize),
        AbsentHttp("HttpGet", o => o.HttpGet),
        AbsentHttp("MimePost", o => o.MimePost),
        AbsentHttp("MimeOptions", o => o.MimeOptions),
        AbsentHttp("HttpPost", o => o.HttpPost),
        AbsentHttp("HttpVersion", o => o.HttpVersion),
        AbsentHttp("Http09Allowed", o => o.Http09Allowed),
        AbsentHttp("IgnoreContentLength", o => o.IgnoreContentLength),
        AbsentHttp("Expect100Timeout", o => o.Expect100Timeout),
        AbsentHttp("Http200Aliases", o => o.Http200Aliases.Count),
        AbsentHttp("RequestTarget", o => o.RequestTarget),
        AbsentHttp("Cookie", o => o.Cookie),
        AbsentHttp("CookieFiles", o => o.CookieFiles.Count),
        AbsentHttp("CookieJar", o => o.CookieJar),
        AbsentHttp("CookieSession", o => o.CookieSession),
        AbsentHttp("CookieList", o => o.CookieList.Count),
        AbsentHttp("AltSvc", o => o.AltSvc),
        AbsentHttp("AltSvcControl", o => o.AltSvcControl),
        AbsentHttp("Hsts", o => o.Hsts),
        AbsentHttp("HstsControl", o => o.HstsControl),
        AbsentHttp("PipeWait", o => o.PipeWait),
        AbsentHttp("StreamDependency", o => o.StreamDependency),
        AbsentHttp("StreamWeight", o => o.StreamWeight),
        AbsentHttp("SslEnableAlpn", o => o.SslEnableAlpn),
    ];

    [TestMethod]
    [DynamicData(nameof(GivenHttpOptions))]
    public void HttpOf_OptionGiven_SetsItsMember(string option, Func<EasyHttpOptions, object?> read, object? expected, string[] arguments)
    {
        EasyHttpOptions http = EasyOptionsFromCommandLine.HttpOf(Parse(arguments));

        Assert.AreEqual(expected, read(http), option);
    }

    [TestMethod]
    [DynamicData(nameof(AbsentHttpOptions))]
    public void HttpOf_OptionAbsent_KeepsLibcurlDefault(string member, Func<EasyHttpOptions, object?> read)
    {
        EasyHttpOptions http = EasyOptionsFromCommandLine.HttpOf(Parse());

        Assert.AreEqual(read(LibcurlHttpDefaults), read(http), member);
    }

    [TestMethod]
    public void HttpOf_NoMaxRedirs_KeepsTheToolsFifty()
    {
        EasyHttpOptions http = EasyOptionsFromCommandLine.HttpOf(Parse());

        Assert.AreEqual(50L, http.MaxRedirects);
    }

    [TestMethod]
    public void HttpOf_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => EasyOptionsFromCommandLine.HttpOf(null!));
    }

    private static string? BodyText(EasyHttpOptions http) =>
        http.PostFields is { } body ? Encoding.UTF8.GetString(body.Span) : null;

    private static object?[] GivenHttp(string option, Func<EasyHttpOptions, object?> read, object? expected, params string[] arguments) =>
        [option, read, expected, arguments];

    private static object?[] AbsentHttp(string member, Func<EasyHttpOptions, object?> read) => [member, read];

    // Each part as [name|file name|type|encoder|headers|data], then |(its subparts) for a multipart.
    private static string? MimeText(EasyHttpOptions http) =>
        http.MimePost is { } body ? MimeBodyText(body) : null;

    private static string MimeBodyText(EasyMimeBody body) =>
        string.Join(' ', body.Parts.Select(part =>
            $"[{part.Name}|{part.FileName}|{part.Type}|{part.Encoder}|{string.Join(',', part.Headers)}|{MimeDataText(part.Data)}"
            + (part.SubParts is { } subParts ? $"|({MimeBodyText(subParts)})]" : "]")));

    private static string MimeDataText(EasyMimeData? data) => data switch
    {
        EasyMimeBytes bytes => "bytes:" + Encoding.UTF8.GetString(bytes.Bytes.Span),
        EasyMimeFile file => "file:" + file.Path,
        EasyMimeStandardInput => "stdin",
        null => "none",
        _ => "other",
    };
}
