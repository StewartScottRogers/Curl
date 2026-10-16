using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Options;

namespace Libcurl.Multi;

/// <summary>
/// <c>curl_multi_setopt</c>'s dispatch (ADR-0582): one <see cref="MultiOptionSetter"/> per
/// <see cref="CurlMultiOption"/>, with curl 8.22.0's <c>multi.c</c> checks.
/// </summary>
internal static class MultiOptionSetters
{
    /// <summary>The <see cref="CurlOptionType"/> of each <c>CURLOPTTYPE_*</c> base, by the option's number over 10000.</summary>
    private static readonly CurlOptionType[] TypesByBase =
        [CurlOptionType.Values, CurlOptionType.Object, CurlOptionType.Function, CurlOptionType.OffT];

    private static readonly Dictionary<CurlMultiOption, MultiOptionSetter> Setters = Create();

    /// <summary><c>CURLMOPT_MAX_CONCURRENT_STREAMS</c>' value outside 1 to <see cref="int.MaxValue"/>.</summary>
    private const long DefaultConcurrentStreams = 100;

    /// <summary>Sets <paramref name="option"/> to <paramref name="value"/> on <paramref name="options"/>.</summary>
    /// <param name="options">The handle's options, replaced by the changed copy on success.</param>
    /// <param name="option">The option to set.</param>
    /// <param name="value">The value to set it to.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.UnknownOption"/> for an option <c>multi.h</c> lacks,
    /// <see cref="CurlMultiCode.BadFunctionArgument"/> for a value of the wrong type, else the row's answer.
    /// </returns>
    internal static CurlMultiCode SetOption(ref MultiOptions options, CurlMultiOption option, object? value)
    {
        if (!Setters.TryGetValue(option, out var setter))
        {
            return CurlMultiCode.UnknownOption;
        }

        return CurlOptionValues.Accepts(TypesByBase[(int)option / 10000], value)
            ? setter(ref options, value)
            : CurlMultiCode.BadFunctionArgument;
    }

    private static Dictionary<CurlMultiOption, MultiOptionSetter> Create()
    {
        var setters = new Dictionary<CurlMultiOption, MultiOptionSetter>();
        AddConnectionSetters(setters);
        AddSharingSetters(setters);
        AddFunctionSetters(setters);
        AddIgnoredSetters(setters);
        return setters;
    }

    private static void AddConnectionSetters(Dictionary<CurlMultiOption, MultiOptionSetter> setters)
    {
        setters[CurlMultiOption.MaxConnects] = Count(uint.MaxValue, static (o, n) => o with { MaxConnects = n });
        setters[CurlMultiOption.MaxHostConnections] = Count(long.MaxValue, static (o, n) => o with { MaxHostConnections = n });
        setters[CurlMultiOption.MaxTotalConnections] = Count(long.MaxValue, static (o, n) => o with { MaxTotalConnections = n });
        setters[CurlMultiOption.MaxConcurrentStreams] = SetConcurrentStreams;
    }

    private static void AddSharingSetters(Dictionary<CurlMultiOption, MultiOptionSetter> setters)
    {
        setters[CurlMultiOption.Pipelining] = Long(static (o, n) => o with { Pipelining = (CurlPipelining)n });
        setters[CurlMultiOption.NetworkChanged] = Long(static (o, n) => o with { NetworkChanged = (CurlNetworkChange)n });
    }

    private static void AddFunctionSetters(Dictionary<CurlMultiOption, MultiOptionSetter> setters)
    {
        setters[CurlMultiOption.SocketFunction] = Function<MultiSocketCallback>(static (o, f) => o with { Socket = f });
        setters[CurlMultiOption.TimerFunction] = Function<MultiTimerCallback>(static (o, f) => o with { Timer = f });
        setters[CurlMultiOption.PushFunction] = Function<MultiPushCallback>(static (o, f) => o with { Push = f });
        setters[CurlMultiOption.NotifyFunction] = Function<MultiNotifyCallback>(static (o, f) => o with { Notify = f });
    }

    /// <summary>
    /// The four <c>*DATA</c> options, whose user data the callback's closure carries, and the five
    /// options libcurl has ignored since 7.62.0: each takes its value and changes nothing.
    /// </summary>
    /// <param name="setters">The rows to add to.</param>
    private static void AddIgnoredSetters(Dictionary<CurlMultiOption, MultiOptionSetter> setters)
    {
        CurlMultiOption[] ignored =
        [
            CurlMultiOption.SocketData, CurlMultiOption.TimerData, CurlMultiOption.PushData, CurlMultiOption.NotifyData,
            CurlMultiOption.MaxPipelineLength, CurlMultiOption.ContentLengthPenaltySize, CurlMultiOption.ChunkLengthPenaltySize,
            CurlMultiOption.PipeliningSiteBlocklist, CurlMultiOption.PipeliningServerBlocklist,
        ];
        foreach (var option in ignored)
        {
            setters[option] = Ignore;
        }
    }

    /// <summary>A <c>long</c> row taking any value, as <c>multi.c</c> does.</summary>
    /// <param name="set">Copies the options with the value set.</param>
    /// <returns>The row.</returns>
    private static MultiOptionSetter Long(Func<MultiOptions, long, MultiOptions> set) =>
        (ref MultiOptions options, object? value) => Set(ref options, set(options, Convert.ToInt64(value)));

    /// <summary>A count row: 0 to <paramref name="maximum"/>, else <see cref="CurlMultiCode.BadFunctionArgument"/>, as <c>multi.c</c>'s <c>curlx_sltoui</c> and <c>_sltouz</c> checks.</summary>
    /// <param name="maximum">The largest count the option takes.</param>
    /// <param name="set">Copies the options with the count set.</param>
    /// <returns>The row.</returns>
    private static MultiOptionSetter Count(long maximum, Func<MultiOptions, long, MultiOptions> set) =>
        (ref MultiOptions options, object? value) =>
        {
            var count = Convert.ToInt64(value);
            return count < 0 || count > maximum
                ? CurlMultiCode.BadFunctionArgument
                : Set(ref options, set(options, count));
        };

    /// <summary>A function row taking a <typeparamref name="T"/> or <see langword="null"/>.</summary>
    /// <typeparam name="T">The callback's delegate type.</typeparam>
    /// <param name="set">Copies the options with the callback set.</param>
    /// <returns>The row; it answers <see cref="CurlMultiCode.BadFunctionArgument"/> for any other delegate.</returns>
    private static MultiOptionSetter Function<T>(Func<MultiOptions, T?, MultiOptions> set)
        where T : Delegate =>
        (ref MultiOptions options, object? value) => value is null or T
            ? Set(ref options, set(options, (T?)value))
            : CurlMultiCode.BadFunctionArgument;

    /// <summary><c>CURLMOPT_MAX_CONCURRENT_STREAMS</c>: 1 to <see cref="int.MaxValue"/>, any other value 100, as <c>multi.c</c> does.</summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="value">The value given.</param>
    /// <returns><see cref="CurlMultiCode.Ok"/>.</returns>
    private static CurlMultiCode SetConcurrentStreams(ref MultiOptions options, object? value)
    {
        var streams = Convert.ToInt64(value);
        var kept = streams is < 1 or > int.MaxValue ? DefaultConcurrentStreams : streams;
        return Set(ref options, options with { MaxConcurrentStreams = kept });
    }

    private static CurlMultiCode Ignore(ref MultiOptions options, object? value) => CurlMultiCode.Ok;

    private static CurlMultiCode Set(ref MultiOptions options, MultiOptions changed)
    {
        options = changed;
        return CurlMultiCode.Ok;
    }
}
