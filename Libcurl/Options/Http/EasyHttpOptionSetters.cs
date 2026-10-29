using System.Globalization;
using System.Text;
using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Http;

namespace Libcurl.Options;

/// <summary>
/// The setter rows of every <c>CURLOPT_*</c> <see cref="EasyHttpOptions"/> holds but
/// <c>CURLOPT_MIMEPOST</c> and <c>CURLOPT_HTTPPOST</c>, each checking its value as curl 8.22.0's
/// <c>lib/setopt.c</c> does.
/// </summary>
internal static class EasyHttpOptionSetters
{
    /// <summary>The largest <c>CURLOPT_EXPECT_100_TIMEOUT_MS</c> kept: libcurl's <c>INT_MAX</c>.</summary>
    private const long MostExpect100TimeoutMilliseconds = int.MaxValue;

    /// <summary>Adds the HTTP rows to <paramref name="setters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    internal static void AddTo(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.Httpheader] = new(SetHttpHeaders);
        setters[CurlOption.Useragent] = new(SetUserAgent);
        setters[CurlOption.Referer] = new(SetReferer);
        setters[CurlOption.Autoreferer] = new(SetAutoReferer);
        setters[CurlOption.AcceptEncoding] = new(SetAcceptEncoding);
        setters[CurlOption.TransferEncoding] = new(SetTransferEncoding);
        setters[CurlOption.HttpContentDecoding] = new(SetHttpContentDecoding);
        setters[CurlOption.HttpTransferDecoding] = new(SetHttpTransferDecoding);
        setters[CurlOption.Followlocation] = new(SetFollowLocation);
        setters[CurlOption.Maxredirs] = new(SetMaxRedirects);
        setters[CurlOption.Postredir] = new(SetPostRedirect);
        setters[CurlOption.Post] = new(SetPost);
        setters[CurlOption.Postfields] = new(SetPostFields);
        setters[CurlOption.Copypostfields] = new(SetCopyPostFields);
        setters[CurlOption.Postfieldsize] = new(SetPostFieldSize);
        setters[CurlOption.PostfieldsizeLarge] = new(SetPostFieldSize);
        setters[CurlOption.Httpget] = new(SetHttpGet);
        setters[CurlOption.MimeOptions] = new(SetMimeOptions);
        setters[CurlOption.HttpVersion] = new(SetHttpVersion);
        setters[CurlOption.Http09Allowed] = new(SetHttp09Allowed);
        setters[CurlOption.IgnoreContentLength] = new(SetIgnoreContentLength);
        setters[CurlOption.Expect100TimeoutMs] = new(SetExpect100Timeout);
        setters[CurlOption.Http200aliases] = new(SetHttp200Aliases);
        setters[CurlOption.RequestTarget] = new(SetRequestTarget);
        setters[CurlOption.Cookie] = new(SetCookie);
        setters[CurlOption.Cookiefile] = new(SetCookieFile);
        setters[CurlOption.Cookiejar] = new(SetCookieJar);
        setters[CurlOption.Cookiesession] = new(SetCookieSession);
        setters[CurlOption.Cookielist] = new(SetCookieList);
        setters[CurlOption.Altsvc] = new(SetAltSvc);
        setters[CurlOption.AltsvcCtrl] = new(SetAltSvcControl);
        setters[CurlOption.Hsts] = new(SetHsts);
        setters[CurlOption.HstsCtrl] = new(SetHstsControl);
        setters[CurlOption.Pipewait] = new(SetPipeWait);
        setters[CurlOption.StreamDepends] = new(SetStreamDepends);
        setters[CurlOption.StreamDependsE] = new(SetStreamDependsExclusive);
        setters[CurlOption.StreamWeight] = new(SetStreamWeight);
        setters[CurlOption.SslEnableAlpn] = new(SetSslEnableAlpn);
    }

    private static CurlCode SetHttpHeaders(ref EasyOptions o, object? v) => Set(ref o, h => h with { HttpHeaders = ListOf(v) });

    private static CurlCode SetUserAgent(ref EasyOptions o, object? v) => Set(ref o, h => h with { UserAgent = (string?)v });

    private static CurlCode SetReferer(ref EasyOptions o, object? v) => Set(ref o, h => h with { Referer = (string?)v });

    private static CurlCode SetAutoReferer(ref EasyOptions o, object? v) => Set(ref o, h => h with { AutoReferer = IsOn(v) });

    private static CurlCode SetAcceptEncoding(ref EasyOptions o, object? v) => Set(ref o, h => h with { AcceptEncoding = (string?)v });

    private static CurlCode SetTransferEncoding(ref EasyOptions o, object? v) => Set(ref o, h => h with { TransferEncoding = IsOn(v) });

    private static CurlCode SetHttpContentDecoding(ref EasyOptions o, object? v) => Set(ref o, h => h with { HttpContentDecoding = IsOn(v) });

    private static CurlCode SetHttpTransferDecoding(ref EasyOptions o, object? v) => Set(ref o, h => h with { HttpTransferDecoding = IsOn(v) });

    private static CurlCode SetMaxRedirects(ref EasyOptions o, object? v) => SetAtLeastMinusOne(ref o, v, (h, n) => h with { MaxRedirects = n });

    private static CurlCode SetPostFields(ref EasyOptions o, object? v) => SetPostBody(ref o, v, copy: false);

    private static CurlCode SetCopyPostFields(ref EasyOptions o, object? v) => SetPostBody(ref o, v, copy: true);

    private static CurlCode SetPostFieldSize(ref EasyOptions o, object? v) => SetAtLeastMinusOne(ref o, v, (h, n) => n > h.PostFieldSize && h.PostFieldsAreCopy ? h with { PostFieldSize = n, PostFields = null, PostFieldsAreCopy = false } : h with { PostFieldSize = n });

    private static CurlCode SetMimeOptions(ref EasyOptions o, object? v) => Set(ref o, h => h with { MimeOptions = (MimeOptions)(NumberOf(v) & (long)MimeOptions.FormEscape) });

    private static CurlCode SetHttp09Allowed(ref EasyOptions o, object? v) => Set(ref o, h => h with { Http09Allowed = IsOn(v) });

    private static CurlCode SetIgnoreContentLength(ref EasyOptions o, object? v) => Set(ref o, h => h with { IgnoreContentLength = IsOn(v) });

    private static CurlCode SetHttp200Aliases(ref EasyOptions o, object? v) => Set(ref o, h => h with { Http200Aliases = ListOf(v) });

    private static CurlCode SetRequestTarget(ref EasyOptions o, object? v) => Set(ref o, h => h with { RequestTarget = (string?)v });

    private static CurlCode SetCookie(ref EasyOptions o, object? v) => Set(ref o, h => h with { Cookie = (string?)v });

    /// <summary><c>CURLOPT_COOKIEFILE</c>: each file is added to the list; <see langword="null"/> clears it.</summary>
    private static CurlCode SetCookieFile(ref EasyOptions o, object? v) => Set(ref o, h => h with { CookieFiles = v is string file ? [.. h.CookieFiles, file] : [] });

    private static CurlCode SetCookieJar(ref EasyOptions o, object? v) => Set(ref o, h => h with { CookieJar = (string?)v });

    private static CurlCode SetCookieSession(ref EasyOptions o, object? v) => Set(ref o, h => h with { CookieSession = IsOn(v) });

    /// <summary><c>CURLOPT_COOKIELIST</c>: each line is added to the list; <see langword="null"/> is ignored.</summary>
    private static CurlCode SetCookieList(ref EasyOptions o, object? v) => Set(ref o, h => v is string line ? h with { CookieList = [.. h.CookieList, line] } : h);

    private static CurlCode SetAltSvc(ref EasyOptions o, object? v) => Set(ref o, h => h with { AltSvc = (string?)v });

    private static CurlCode SetAltSvcControl(ref EasyOptions o, object? v) => NumberOf(v) == 0 ? CurlCode.BadFunctionArgument : Set(ref o, h => h with { AltSvcControl = (AltSvcControl)NumberOf(v) });

    private static CurlCode SetHsts(ref EasyOptions o, object? v) => Set(ref o, h => h with { Hsts = (string?)v });

    private static CurlCode SetHstsControl(ref EasyOptions o, object? v) => Set(ref o, h => h with { HstsControl = HstsControlOf(NumberOf(v)) });

    private static CurlCode SetPipeWait(ref EasyOptions o, object? v) => Set(ref o, h => h with { PipeWait = IsOn(v) });

    private static CurlCode SetStreamDepends(ref EasyOptions o, object? v) => Set(ref o, h => h with { StreamDependency = DependencyOn(v, exclusive: false) });

    private static CurlCode SetStreamDependsExclusive(ref EasyOptions o, object? v) => Set(ref o, h => h with { StreamDependency = DependencyOn(v, exclusive: true) });

    private static CurlCode SetSslEnableAlpn(ref EasyOptions o, object? v) => Set(ref o, h => h with { SslEnableAlpn = IsOn(v) });

    /// <summary>Replaces the handle's <see cref="EasyHttpOptions"/> with the copy <paramref name="change"/> returns; always <see cref="CurlCode.Ok"/>.</summary>
    private static CurlCode Set(ref EasyOptions options, Func<EasyHttpOptions, EasyHttpOptions> change)
    {
        options = options with { Http = change(options.Http) };
        return CurlCode.Ok;
    }

    /// <summary>A value of -1 or more (-1 meaning unlimited or unknown); a smaller one is <see cref="CurlCode.BadFunctionArgument"/>.</summary>
    private static CurlCode SetAtLeastMinusOne(ref EasyOptions options, object? value, Func<EasyHttpOptions, long, EasyHttpOptions> change)
    {
        var number = NumberOf(value);
        return number < -1 ? CurlCode.BadFunctionArgument : Set(ref options, h => change(h, number));
    }

    /// <summary><c>CURLOPT_FOLLOWLOCATION</c>: 0 to 3 (<c>CURLFOLLOW_*</c>), else <see cref="CurlCode.BadFunctionArgument"/>.</summary>
    private static CurlCode SetFollowLocation(ref EasyOptions options, object? value)
    {
        var number = NumberOf(value);
        return number is < 0 or > (long)FollowLocation.FirstOnly
            ? CurlCode.BadFunctionArgument
            : Set(ref options, h => h with { FollowLocation = (FollowLocation)number });
    }

    /// <summary><c>CURLOPT_POSTREDIR</c>: a negative value is refused; only the <c>CURL_REDIR_POST_*</c> bits are kept.</summary>
    private static CurlCode SetPostRedirect(ref EasyOptions options, object? value)
    {
        var number = NumberOf(value);
        return number < 0
            ? CurlCode.BadFunctionArgument
            : Set(ref options, h => h with { PostRedirect = (PostRedirect)(number & (long)PostRedirect.All) });
    }

    /// <summary>
    /// <c>CURLOPT_POST</c>: non-zero makes the request a <c>POST</c> and clears <c>CURLOPT_NOBODY</c>; zero makes it a <c>GET</c>.
    /// Either clears <c>CURLOPT_UPLOAD</c>, as libcurl's one request method replaces its <c>PUT</c> (upstream test1948, BL-2679).
    /// </summary>
    private static CurlCode SetPost(ref EasyOptions options, object? value)
    {
        options = options with { Transfer = options.Transfer with { Upload = false } };
        if (!IsOn(value))
        {
            return Set(ref options, h => h with { Post = false });
        }

        options = options with { Transfer = options.Transfer with { NoBody = false } };
        return Set(ref options, h => h with { Post = true, HttpGet = false });
    }

    /// <summary>
    /// <c>CURLOPT_POSTFIELDS</c> keeps the caller's buffer by reference; <c>CURLOPT_COPYPOSTFIELDS</c>
    /// copies it, only the first <c>CURLOPT_POSTFIELDSIZE</c> bytes when that is set; setting <c>CURLOPT_POSTFIELDSIZE</c> afterwards discards that copy. Either makes the
    /// request a <c>POST</c>, clearing <c>CURLOPT_UPLOAD</c> as <c>CURLOPT_POST</c> does. A string is sent as its UTF-8 bytes.
    /// </summary>
    private static CurlCode SetPostBody(ref EasyOptions options, object? value, bool copy)
    {
        var fields = value switch
        {
            byte[] bytes => new ReadOnlyMemory<byte>(bytes),
            ReadOnlyMemory<byte> memory => memory,
            string text => Encoding.UTF8.GetBytes(text),
            _ => default(ReadOnlyMemory<byte>?),
        };
        if (fields is null && value is not null)
        {
            return CurlCode.BadFunctionArgument;
        }

        options = options with { Transfer = options.Transfer with { Upload = false } };
        return Set(ref options, h => h with { PostFields = copy ? CopyOf(fields, h.PostFieldSize) : fields, PostFieldsAreCopy = copy && fields is not null, Post = true, HttpGet = false });
    }

    /// <summary>A private copy of <paramref name="fields"/>, cut to <paramref name="size"/> bytes when that is not negative and shorter; <see langword="null"/> stays <see langword="null"/>.</summary>
    private static ReadOnlyMemory<byte>? CopyOf(ReadOnlyMemory<byte>? fields, long size) =>
        fields is { } bytes ? bytes[..(int)Math.Min(size < 0 ? bytes.Length : size, bytes.Length)].ToArray() : (ReadOnlyMemory<byte>?)null;

    /// <summary><c>CURLOPT_HTTPGET</c>: non-zero makes the request a <c>GET</c>, clearing <c>CURLOPT_POST</c>, <c>CURLOPT_NOBODY</c> and <c>CURLOPT_UPLOAD</c>; zero changes nothing.</summary>
    private static CurlCode SetHttpGet(ref EasyOptions options, object? value)
    {
        if (!IsOn(value))
        {
            return CurlCode.Ok;
        }

        options = options with { Transfer = options.Transfer with { NoBody = false, Upload = false } };
        return Set(ref options, h => h with { HttpGet = true, Post = false });
    }

    /// <summary>
    /// <c>CURLOPT_HTTP_VERSION</c>: a <c>CURL_HTTP_VERSION_*</c> value (every one is built in); a
    /// negative value is <see cref="CurlCode.BadFunctionArgument"/> and any other <see cref="CurlCode.UnsupportedProtocol"/>.
    /// </summary>
    private static CurlCode SetHttpVersion(ref EasyOptions options, object? value)
    {
        var number = NumberOf(value);
        if (number < 0)
        {
            return CurlCode.BadFunctionArgument;
        }

        var version = (HttpVersion)number;
        return Enum.IsDefined(version) ? Set(ref options, h => h with { HttpVersion = version }) : CurlCode.UnsupportedProtocol;
    }

    /// <summary><c>CURLOPT_EXPECT_100_TIMEOUT_MS</c>: a negative value is refused; one past <see cref="int.MaxValue"/> is cut to it.</summary>
    private static CurlCode SetExpect100Timeout(ref EasyOptions options, object? value)
    {
        var milliseconds = NumberOf(value);
        return milliseconds < 0
            ? CurlCode.BadFunctionArgument
            : Set(ref options, h => h with { Expect100Timeout = TimeSpan.FromMilliseconds(Math.Min(milliseconds, MostExpect100TimeoutMilliseconds)) });
    }

    /// <summary><c>CURLOPT_STREAM_WEIGHT</c>: 1 to 256 is kept; any other value is ignored, as libcurl does.</summary>
    private static CurlCode SetStreamWeight(ref EasyOptions options, object? value)
    {
        var weight = NumberOf(value);
        return weight is < 1 or > 256 ? CurlCode.Ok : Set(ref options, h => h with { StreamWeight = (int)weight });
    }

    /// <summary>The <c>CURLOPT_HSTS_CTRL</c> flags kept: none unless <c>CURLHSTS_ENABLE</c> is set, then only it and <c>CURLHSTS_READONLYFILE</c>.</summary>
    private static HstsControl HstsControlOf(long number) =>
        (number & (long)HstsControl.Enable) == 0 ? HstsControl.None : (HstsControl)(number & (long)(HstsControl.Enable | HstsControl.ReadOnlyFile));

    /// <summary>The HTTP/2 stream dependency on the <c>CURL *</c> handle <paramref name="parent"/>, or none for <see langword="null"/>.</summary>
    private static EasyStreamDependency? DependencyOn(object? parent, bool exclusive) =>
        parent is null ? null : new EasyStreamDependency(parent, exclusive);

    /// <summary>A private copy of a string list option (<c>curl_slist</c>), so a later change by the caller does not alter the handle; <see langword="null"/> is an empty list.</summary>
    private static IReadOnlyList<string> ListOf(object? value) => value is IEnumerable<string> lines ? [.. lines] : [];

    /// <summary>Whether a <c>CURLOT_LONG</c> on/off value is on: any value but 0, as in libcurl.</summary>
    private static bool IsOn(object? value) => NumberOf(value) != 0;

    private static long NumberOf(object? value) => Convert.ToInt64(value, CultureInfo.InvariantCulture);
}
