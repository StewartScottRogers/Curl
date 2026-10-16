using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.Callbacks.Progress;

/// <summary>
/// The setter rows of <c>CURLOPT_PROGRESSFUNCTION</c>, <c>CURLOPT_XFERINFOFUNCTION</c> and
/// <c>CURLOPT_DEBUGFUNCTION</c> and their <c>*DATA</c> pointers (ADR-0574). A function row takes its own delegate
/// type or <see langword="null"/> (libcurl's default) and answers <see cref="CurlCode.BadFunctionArgument"/> for any
/// other delegate. The engine calls them as libcurl does: the xferinfo function, else the progress function, on
/// every progress report unless <c>CURLOPT_NOPROGRESS</c> is set, a non-zero return aborting the transfer with
/// <see cref="CurlCode.AbortedByCallback"/>, and the debug function under <c>CURLOPT_VERBOSE</c>. The managed
/// callback carries its user data in its closure, and libcurl's default functions read no <c>*DATA</c>, so
/// <c>CURLOPT_PROGRESSDATA</c> (the same option as <c>CURLOPT_XFERINFODATA</c>) and <c>CURLOPT_DEBUGDATA</c> take any
/// value and change nothing.
/// </summary>
internal static class EasyProgressCallbackSetters
{
    /// <summary>Adds the progress, xferinfo and debug rows to <paramref name="setters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    internal static void AddTo(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.Progressfunction] = new(SetProgressFunction);
        setters[CurlOption.Xferinfofunction] = new(SetTransferInfoFunction);
        setters[CurlOption.Debugfunction] = new(SetDebugFunction);
        setters[CurlOption.Xferinfodata] = new(TakeUserData);
        setters[CurlOption.Debugdata] = new(TakeUserData);
    }

    private static CurlCode SetProgressFunction(ref EasyOptions options, object? value) =>
        value is null or ProgressCallback
            ? SetCallbacks(ref options, options.Callbacks with { Progress = (ProgressCallback?)value })
            : CurlCode.BadFunctionArgument;

    private static CurlCode SetTransferInfoFunction(ref EasyOptions options, object? value) =>
        value is null or TransferInfoCallback
            ? SetCallbacks(ref options, options.Callbacks with { TransferInfo = (TransferInfoCallback?)value })
            : CurlCode.BadFunctionArgument;

    private static CurlCode SetDebugFunction(ref EasyOptions options, object? value) =>
        value is null or DebugCallback
            ? SetCallbacks(ref options, options.Callbacks with { Debug = (DebugCallback?)value })
            : CurlCode.BadFunctionArgument;

    private static CurlCode TakeUserData(ref EasyOptions options, object? value) => CurlCode.Ok;

    private static CurlCode SetCallbacks(ref EasyOptions options, EasyCallbackOptions callbacks)
    {
        options = options with { Callbacks = callbacks };
        return CurlCode.Ok;
    }
}
