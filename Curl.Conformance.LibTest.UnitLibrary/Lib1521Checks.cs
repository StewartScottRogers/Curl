using Curl.Api.Codes;
using Libcurl.Info;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// The body <c>mk-lib1521.pl</c> generates into <c>lib1521.c</c>: for each option, the first
/// <c>curl_easy_setopt</c> with the test value of the option's type, and, unless that answered
/// <c>CURLE_NOT_BUILT_IN</c> or <c>CURLE_UNKNOWN_OPTION</c>, the secondary ones; then a
/// <c>curl_easy_getinfo</c> for each info. Every answer outside what upstream allows writes one
/// <c>... returned N, "text" on line L</c> line to the program's stdout, where L, upstream's
/// <c>__LINE__</c>, is the number of the call that answered (C# has no generated source to count lines in).
/// </summary>
/// <param name="setOption">The handle's <c>curl_easy_setopt</c>.</param>
/// <param name="getInfo">The handle's <c>curl_easy_getinfo</c>, its value thrown away.</param>
/// <param name="values">The object values the program passes.</param>
/// <param name="output">The program's stdout.</param>
public sealed class Lib1521Checks(
    Func<CurlOption, object?, CurlCode> setOption,
    Func<CurlInfo, CurlCode> getInfo,
    Lib1521Values values,
    TextWriter output)
{
    /// <summary><c>LONG_MIN</c>: Libcurl's <c>long</c> is a <see cref="long"/>.</summary>
    private const long LongLow = long.MinValue;

    /// <summary><c>LONG_MAX</c>.</summary>
    private const long LongHigh = long.MaxValue;

    private static readonly long[] LongValues = [22L, LongLow, -1L, LongHigh];

    private static readonly long[] OffTValues = [22L, LongHigh, LongLow];

    private static readonly byte[] Blob = "silly"u8.ToArray();

    /// <summary><c>errstring</c>'s answers beyond <c>CURLE_OK</c>, each with the options allowed to give it.</summary>
    private static readonly Dictionary<CurlCode, HashSet<CurlOption>> StringErrors = new()
    {
        [CurlCode.BadFunctionArgument] =
        [
            CurlOption.DnsLocalIp4, CurlOption.DnsLocalIp6, CurlOption.DnsServers, CurlOption.Ech,
            CurlOption.ProxyTlsauthType, CurlOption.Sslengine, CurlOption.TlsauthType,
        ],
        [CurlCode.UnsupportedProtocol] = [CurlOption.ProtocolsStr, CurlOption.RedirProtocolsStr],
        [CurlCode.SslEngineNotFound] = [CurlOption.Sslengine],
    };

    /// <summary><c>bad_neg</c>'s options.</summary>
    private static readonly HashSet<CurlOption> MinusOneOptions =
    [
        CurlOption.DnsCacheTimeout, CurlOption.Infilesize, CurlOption.InfilesizeLarge, CurlOption.Maxredirs,
        CurlOption.Postfieldsize, CurlOption.PostfieldsizeLarge, CurlOption.ResumeFrom, CurlOption.ResumeFromLarge,
    ];

    /// <summary>The options the generator sets <c>CURLOPT_POSTFIELDSIZE</c> to 0 before.</summary>
    private static readonly HashSet<CurlOption> PostfieldsOptions = [CurlOption.Postfields, CurlOption.Copypostfields];

    /// <summary>The program's <c>result</c>, which only the secondary setopts and the getinfos assign.</summary>
    private CurlCode result;

    private int calls;

    /// <summary>
    /// Every option of the options table but its aliases, in <c>curl.h</c>'s order (by the option's own number).
    /// </summary>
    public static IEnumerable<CurlEasyOption> Options =>
        CurlOptions.All.Where(static o => !o.IsAlias).OrderBy(static o => (int)o.Id % 10000);

    /// <summary>Runs the checks of every option of <see cref="Options"/>, then of every info of <see cref="Lib1521Infos.All"/>.</summary>
    public void VerifyAll()
    {
        foreach (CurlEasyOption option in Options)
        {
            VerifyOption(option);
        }

        foreach ((CurlInfo info, string name) in Lib1521Infos.All)
        {
            VerifyInfo(info, name);
        }
    }

    /// <summary>The checks of one option, by its type.</summary>
    /// <param name="option">The option's row.</param>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> is <see langword="null"/>.</exception>
    public void VerifyOption(CurlEasyOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        string name = "CURLOPT_" + option.Name;
        switch (option.Type)
        {
            case CurlOptionType.String:
                VerifyString(name, option.Id);
                break;
            case CurlOptionType.Long or CurlOptionType.Values:
                VerifyNumber(name, option.Id, LongValues);
                break;
            case CurlOptionType.OffT:
                VerifyNumber(name, option.Id, OffTValues);
                break;
            case CurlOptionType.Blob:
                VerifyBlob(name, option.Id);
                break;
            default:
                VerifyPointer(name, option.Id, FirstPointer(option));
                break;
        }
    }

    /// <summary>One <c>curl_easy_getinfo</c>: <c>t1521_geterr</c> when it does not answer <c>CURLE_OK</c>.</summary>
    /// <param name="info">The info.</param>
    /// <param name="name">Its upstream name without <c>CURLINFO_</c>.</param>
    public void VerifyInfo(CurlInfo info, string name)
    {
        result = Count(getInfo(info));
        ReportIf(result != CurlCode.Ok, "CURLINFO_" + name, result);
    }

    /// <summary>
    /// <c>present(x)</c>: whether the first setopt's answer says the option exists in this build.
    /// </summary>
    /// <param name="code">The first setopt's answer.</param>
    /// <returns>Neither <c>CURLE_NOT_BUILT_IN</c> nor <c>CURLE_UNKNOWN_OPTION</c>.</returns>
    public static bool IsPresent(CurlCode code) => code is not (CurlCode.NotBuiltIn or CurlCode.UnknownOption);

    /// <summary>
    /// <c>errstring</c>'s allowed answers: the options that may refuse a string they do not recognize.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="code">Its answer.</param>
    /// <returns>Whether the answer is allowed.</returns>
    public static bool IsAllowedStringError(CurlOption option, CurlCode code) =>
        StringErrors.TryGetValue(code, out HashSet<CurlOption>? options) && options.Contains(option);

    /// <summary>
    /// <c>errlong</c>'s allowed answers beyond <c>bad_long</c>'s <c>CURLE_BAD_FUNCTION_ARGUMENT</c>.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="code">Its answer.</param>
    /// <returns>Whether the answer is allowed.</returns>
    public static bool IsAllowedNumberError(CurlOption option, CurlCode code) => code switch
    {
        CurlCode.BadFunctionArgument => true,
        CurlCode.UnsupportedProtocol => option is CurlOption.HttpVersion,
        CurlCode.NotBuiltIn => option is CurlOption.Httpauth or CurlOption.Proxyauth or CurlOption.Socks5Auth,
        _ => false,
    };

    /// <summary><c>bad_neg</c>: the options that must take -1.</summary>
    /// <param name="option">The option.</param>
    /// <returns>Whether a failing -1 is reported.</returns>
    public static bool MustTakeMinusOne(CurlOption option) => MinusOneOptions.Contains(option);

    private void VerifyString(string name, CurlOption option)
    {
        CurlCode first = Set(option, "string");
        ReportIf(first != CurlCode.Ok && IsPresent(first) && !IsAllowedStringError(option, first), name + " set to a string", first);
        if (IsPresent(first))
        {
            _ = Set(option, string.Empty);
            VerifyNull(name, option);
        }
    }

    private void VerifyNumber(string name, CurlOption option, long[] secondary)
    {
        CurlCode first = Set(option, 0L);
        if (!IsPresent(first))
        {
            return;
        }

        ReportIf(first is not (CurlCode.Ok or CurlCode.BadFunctionArgument), name + " set to 0", first);
        foreach (long value in secondary)
        {
            VerifySecondaryNumber(name, option, value);
        }
    }

    /// <summary>One secondary number setopt: -1 is reported only for <c>bad_neg</c>'s options, any other value by <c>errlong</c>.</summary>
    private void VerifySecondaryNumber(string name, CurlOption option, long value)
    {
        result = Set(option, value);
        if (result == CurlCode.Ok)
        {
            return;
        }

        if (value == -1L)
        {
            ReportIf(MustTakeMinusOne(option), name + " set to -1", result);
            return;
        }

        ReportIf(!IsAllowedNumberError(option, result), name + " set to non-zero", result);
    }

    /// <summary>The blob check, which upstream's generator writes over <c>result</c>, not <c>first</c>.</summary>
    private void VerifyBlob(string name, CurlOption option)
    {
        CurlCode first = Set(option, Blob);
        ReportIf(result != CurlCode.Ok, name, result);
        if (IsPresent(first))
        {
            VerifyNull(name, option);
        }
    }

    private void VerifyPointer(string name, CurlOption option, object? value)
    {
        CurlCode first = Set(option, value);
        ReportIf(first != CurlCode.Ok && IsPresent(first), name, first);
        if (IsPresent(first))
        {
            VerifyNull(name, option);
        }
    }

    private void VerifyNull(string name, CurlOption option)
    {
        result = Set(option, null);
        ReportIf(result != CurlCode.Ok, name + " set to NULL", result);
    }

    /// <summary>
    /// The first value of a pointer option: <c>NULL</c> for an slist, a function or <c>CURLOPT_HTTPPOST</c> and
    /// <c>CURLOPT_MIMEPOST</c> (their variables are never set), else the object the generator names. Before
    /// <c>CURLOPT_POSTFIELDS</c> and <c>CURLOPT_COPYPOSTFIELDS</c> it sets <c>CURLOPT_POSTFIELDSIZE</c> to 0.
    /// </summary>
    private object? FirstPointer(CurlEasyOption option)
    {
        if (option.Type is CurlOptionType.SList or CurlOptionType.Function)
        {
            return null;
        }

        if (PostfieldsOptions.Contains(option.Id))
        {
            _ = Set(CurlOption.Postfieldsize, 0L);
            return "moooo";
        }

        return values.ObjectFor(option.Id);
    }

    private CurlCode Set(CurlOption option, object? value) => Count(setOption(option, value));

    private CurlCode Count(CurlCode code)
    {
        calls++;
        return code;
    }

    private void ReportIf(bool failed, string what, CurlCode code)
    {
        if (failed)
        {
            output.WriteLine($"{what} returned {(int)code}, \"{CurlErrorText.Easy(code)}\" on line {calls}");
        }
    }
}
