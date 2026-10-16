using System.Globalization;
using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;

namespace Libcurl.Options;

/// <summary>
/// The setter rows of every <c>CURLOPT_*</c> <see cref="EasyTransferOptions"/> holds, each checking
/// its value as curl 8.22.0's <c>lib/setopt.c</c> does (ADR-0572); a <c>_LARGE</c> or <c>_MS</c>
/// alias sets the same field as the option it extends.
/// </summary>
internal static class EasyTransferOptionSetters
{
    /// <summary>The largest count a time option takes before it is cut: libcurl's <c>INT_MAX</c>.</summary>
    private const long MostCount = int.MaxValue;

    /// <summary>The receive buffer's default, <c>READBUFFER_SIZE</c>: 16384 bytes.</summary>
    private const long DefaultBufferSize = 16384;

    /// <summary>The smallest receive buffer, <c>READBUFFER_MIN</c>: 1024 bytes.</summary>
    private const long LeastBufferSize = 1024;

    /// <summary>The largest receive buffer, <c>READBUFFER_MAX</c>: 10 MB.</summary>
    private const long MostBufferSize = 10 * 1024 * 1024;

    /// <summary>The smallest upload buffer, <c>UPLOADBUFFER_MIN</c>: 16384 bytes.</summary>
    private const long LeastUploadBufferSize = 16384;

    /// <summary>The largest upload buffer, <c>UPLOADBUFFER_MAX</c>: 2 MB.</summary>
    private const long MostUploadBufferSize = 2 * 1024 * 1024;

    /// <summary>Adds the transfer rows to <paramref name="setters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    internal static void AddTo(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        AddRequestSetters(setters);
        AddSizeSetters(setters);
        AddSpeedAndTimeSetters(setters);
    }

    private static void AddRequestSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.Range] = new(SetRange);
        setters[CurlOption.ResumeFrom] = new(SetResumeFrom);
        setters[CurlOption.ResumeFromLarge] = new(SetResumeFrom);
        setters[CurlOption.Customrequest] = new(SetCustomRequest);
        setters[CurlOption.Filetime] = new(SetFileTime);
        setters[CurlOption.Dirlistonly] = new(SetDirListOnly);
        setters[CurlOption.Nobody] = new(SetNoBody);
        setters[CurlOption.Upload] = new(SetUpload);
        setters[CurlOption.Transfertext] = new(SetTransferText);
        setters[CurlOption.Crlf] = new(SetCrlf);
        setters[CurlOption.ProxyTransferMode] = new(SetProxyTransferMode);
        setters[CurlOption.Timecondition] = new(SetTimeCondition);
        setters[CurlOption.Timevalue] = new(SetTimeValue);
        setters[CurlOption.TimevalueLarge] = new(SetTimeValue);
    }

    private static void AddSizeSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.Infilesize] = new(SetInFileSize);
        setters[CurlOption.InfilesizeLarge] = new(SetInFileSize);
        setters[CurlOption.Buffersize] = new(SetBufferSize);
        setters[CurlOption.UploadBuffersize] = new(SetUploadBufferSize);
        setters[CurlOption.Maxfilesize] = new(SetMaxFileSize);
        setters[CurlOption.MaxfilesizeLarge] = new(SetMaxFileSize);
    }

    private static void AddSpeedAndTimeSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.LowSpeedLimit] = new(SetLowSpeedLimit);
        setters[CurlOption.LowSpeedTime] = new(SetLowSpeedTime);
        setters[CurlOption.MaxSendSpeedLarge] = new(SetMaxSendSpeed);
        setters[CurlOption.MaxRecvSpeedLarge] = new(SetMaxReceiveSpeed);
        setters[CurlOption.Timeout] = new(SetTimeoutSeconds);
        setters[CurlOption.TimeoutMs] = new(SetTimeoutMilliseconds);
    }

    private static CurlCode SetRange(ref EasyOptions o, object? v) => Set(ref o, t => t with { Range = (string?)v });

    private static CurlCode SetResumeFrom(ref EasyOptions o, object? v) => SetFrom(ref o, v, -1, (t, offset) => t with { ResumeFrom = offset });

    private static CurlCode SetCustomRequest(ref EasyOptions o, object? v) => Set(ref o, t => t with { CustomRequest = (string?)v });

    private static CurlCode SetFileTime(ref EasyOptions o, object? v) => Set(ref o, t => t with { FileTime = IsOn(v) });

    private static CurlCode SetDirListOnly(ref EasyOptions o, object? v) => Set(ref o, t => t with { DirListOnly = IsOn(v) });

    private static CurlCode SetNoBody(ref EasyOptions o, object? v) => Set(ref o, t => t with { NoBody = IsOn(v) });

    private static CurlCode SetUpload(ref EasyOptions o, object? v) => Set(ref o, t => t with { Upload = IsOn(v) });

    private static CurlCode SetTransferText(ref EasyOptions o, object? v) => Set(ref o, t => t with { TransferText = IsOn(v) });

    private static CurlCode SetCrlf(ref EasyOptions o, object? v) => Set(ref o, t => t with { Crlf = IsOn(v) });

    /// <summary><c>CURLOPT_PROXY_TRANSFER_MODE</c>: 0 or 1 only; any other value is <see cref="CurlCode.BadFunctionArgument"/>.</summary>
    private static CurlCode SetProxyTransferMode(ref EasyOptions o, object? v) =>
        SetInRange(ref o, v, 0, 1, (t, mode) => t with { ProxyTransferMode = mode == 1 });

    /// <summary><c>CURLOPT_TIMECONDITION</c>: <c>CURL_TIMECOND_NONE</c> to <c>_LASTMOD</c>; any other value is <see cref="CurlCode.BadFunctionArgument"/>.</summary>
    private static CurlCode SetTimeCondition(ref EasyOptions o, object? v) =>
        SetInRange(ref o, v, (long)TimeCondition.None, (long)TimeCondition.LastModified, (t, condition) => t with { TimeCondition = (TimeCondition)condition });

    /// <summary><c>CURLOPT_TIMEVALUE</c> and <c>_LARGE</c>: any <c>time_t</c>, before 1970 included.</summary>
    private static CurlCode SetTimeValue(ref EasyOptions o, object? v) => Set(ref o, t => t with { TimeValue = NumberOf(v) });

    private static CurlCode SetInFileSize(ref EasyOptions o, object? v) => SetFrom(ref o, v, -1, (t, size) => t with { InFileSize = size });

    private static CurlCode SetMaxFileSize(ref EasyOptions o, object? v) => SetFrom(ref o, v, 0, (t, size) => t with { MaxFileSize = size });

    private static CurlCode SetLowSpeedLimit(ref EasyOptions o, object? v) => SetFrom(ref o, v, 0, (t, speed) => t with { LowSpeedLimit = speed });

    private static CurlCode SetMaxSendSpeed(ref EasyOptions o, object? v) => SetFrom(ref o, v, 0, (t, speed) => t with { MaxSendSpeed = speed });

    private static CurlCode SetMaxReceiveSpeed(ref EasyOptions o, object? v) => SetFrom(ref o, v, 0, (t, speed) => t with { MaxReceiveSpeed = speed });

    private static CurlCode SetLowSpeedTime(ref EasyOptions o, object? v) => SetCount(ref o, v, (t, seconds) => t with { LowSpeedTime = TimeSpan.FromSeconds(seconds) });

    private static CurlCode SetTimeoutSeconds(ref EasyOptions o, object? v) => SetCount(ref o, v, (t, seconds) => t with { Timeout = TimeSpan.FromSeconds(seconds) });

    private static CurlCode SetTimeoutMilliseconds(ref EasyOptions o, object? v) => SetCount(ref o, v, (t, ms) => t with { Timeout = TimeSpan.FromMilliseconds(ms) });

    /// <summary>
    /// <c>CURLOPT_BUFFERSIZE</c>: below 1 is the 16384-byte default, below 1024 is raised to 1024,
    /// and past 10 MB is cut to it; never an error.
    /// </summary>
    private static CurlCode SetBufferSize(ref EasyOptions o, object? v)
    {
        var size = NumberOf(v);
        var kept = size < 1 ? DefaultBufferSize : Math.Clamp(size, LeastBufferSize, MostBufferSize);
        return Set(ref o, t => t with { BufferSize = kept });
    }

    /// <summary><c>CURLOPT_UPLOAD_BUFFERSIZE</c>: held between 16384 bytes and 2 MB; never an error.</summary>
    private static CurlCode SetUploadBufferSize(ref EasyOptions o, object? v) =>
        Set(ref o, t => t with { UploadBufferSize = Math.Clamp(NumberOf(v), LeastUploadBufferSize, MostUploadBufferSize) });

    private static CurlCode Set(ref EasyOptions options, Func<EasyTransferOptions, EasyTransferOptions> change)
    {
        options = options with { Transfer = change(options.Transfer) };
        return CurlCode.Ok;
    }

    /// <summary>A value from <paramref name="least"/> up; a smaller one is <see cref="CurlCode.BadFunctionArgument"/>.</summary>
    private static CurlCode SetFrom(ref EasyOptions options, object? value, long least, Func<EasyTransferOptions, long, EasyTransferOptions> change) =>
        SetInRange(ref options, value, least, long.MaxValue, change);

    /// <summary>A time from 0, cut to <c>INT_MAX</c>; a negative value is <see cref="CurlCode.BadFunctionArgument"/>.</summary>
    private static CurlCode SetCount(ref EasyOptions options, object? value, Func<EasyTransferOptions, long, EasyTransferOptions> change) =>
        SetFrom(ref options, value, 0, (t, number) => change(t, Math.Min(number, MostCount)));

    /// <summary>A value from <paramref name="least"/> to <paramref name="most"/>; any other is <see cref="CurlCode.BadFunctionArgument"/>.</summary>
    private static CurlCode SetInRange(ref EasyOptions options, object? value, long least, long most, Func<EasyTransferOptions, long, EasyTransferOptions> change)
    {
        var number = NumberOf(value);
        return number < least || number > most ? CurlCode.BadFunctionArgument : Set(ref options, t => change(t, number));
    }

    private static bool IsOn(object? value) => NumberOf(value) != 0;

    private static long NumberOf(object? value) => Convert.ToInt64(value, CultureInfo.InvariantCulture);
}
