using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Har;

/// <summary>
/// The hook an option gives when its path is the empty string: it refuses every transfer with exit 2, naming the
/// option, without contacting the server, rather than let the empty path reach the file system.
/// </summary>
/// <param name="optionName">The long name of the option, without its leading <c>--</c>.</param>
internal sealed class EmptyPathTransferHook(string optionName) : ITransferHook
{
    /// <summary>Gets the message the refused transfer reports.</summary>
    public string Message { get; } = "--" + optionName + " expects a non-empty path";

    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next) =>
        ValueTask.FromResult(TransferResult.Failure(CurlExitCode.FailedInit, Message));
}
