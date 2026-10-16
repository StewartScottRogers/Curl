using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.Callbacks.ReadWrite;

/// <summary>
/// The setter rows of <c>CURLOPT_WRITEFUNCTION</c>, <c>CURLOPT_READFUNCTION</c> and <c>CURLOPT_HEADERFUNCTION</c>
/// and their <c>*DATA</c> pointers (ADR-0569). A function row takes its own delegate type or <see langword="null"/>
/// (libcurl's default) and answers <see cref="CurlCode.BadFunctionArgument"/> for any other delegate. The managed
/// callback carries its user data in its closure, so a <c>*DATA</c> row matters only as the <c>FILE *</c> of
/// libcurl's default function: a <see cref="Stream"/> installs <see cref="StreamDataCallbacks"/> over it unless a
/// function of the caller's own is set, and any other value is taken and changes nothing.
/// </summary>
internal static class EasyReadWriteCallbackSetters
{
    /// <summary>Adds the write, read and header rows to <paramref name="setters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    internal static void AddTo(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.Writefunction] = new(SetWriteFunction);
        setters[CurlOption.Readfunction] = new(SetReadFunction);
        setters[CurlOption.Headerfunction] = new(SetHeaderFunction);
        setters[CurlOption.Writedata] = new(SetWriteData);
        setters[CurlOption.Readdata] = new(SetReadData);
        setters[CurlOption.Headerdata] = new(SetHeaderData);
    }

    private static CurlCode SetWriteFunction(ref EasyOptions options, object? value) =>
        value is null or WriteCallback
            ? SetCallbacks(ref options, options.Callbacks with { Write = (WriteCallback?)value })
            : CurlCode.BadFunctionArgument;

    private static CurlCode SetReadFunction(ref EasyOptions options, object? value) =>
        value is null or ReadCallback
            ? SetCallbacks(ref options, options.Callbacks with { Read = (ReadCallback?)value })
            : CurlCode.BadFunctionArgument;

    private static CurlCode SetHeaderFunction(ref EasyOptions options, object? value) =>
        value is null or HeaderCallback
            ? SetCallbacks(ref options, options.Callbacks with { Header = (HeaderCallback?)value })
            : CurlCode.BadFunctionArgument;

    private static CurlCode SetWriteData(ref EasyOptions options, object? value) =>
        value is Stream stream && IsDefaultFunction(options.Callbacks.Write)
            ? SetCallbacks(ref options, options.Callbacks with { Write = new StreamDataCallbacks(stream).Write })
            : CurlCode.Ok;

    private static CurlCode SetReadData(ref EasyOptions options, object? value) =>
        value is Stream stream && IsDefaultFunction(options.Callbacks.Read)
            ? SetCallbacks(ref options, options.Callbacks with { Read = new StreamDataCallbacks(stream).Read })
            : CurlCode.Ok;

    private static CurlCode SetHeaderData(ref EasyOptions options, object? value) =>
        value is Stream stream && IsDefaultFunction(options.Callbacks.Header)
            ? SetCallbacks(ref options, options.Callbacks with { Header = new StreamDataCallbacks(stream).Write })
            : CurlCode.Ok;

    /// <summary>Whether <paramref name="callback"/> is unset or libcurl's default over a <c>*DATA</c> stream, not the caller's own function.</summary>
    /// <param name="callback">The callback set.</param>
    /// <returns><see langword="true"/> when a <c>*DATA</c> stream may replace it.</returns>
    private static bool IsDefaultFunction(Delegate? callback) => callback is null || callback.Target is StreamDataCallbacks;

    private static CurlCode SetCallbacks(ref EasyOptions options, EasyCallbackOptions callbacks)
    {
        options = options with { Callbacks = callbacks };
        return CurlCode.Ok;
    }
}
