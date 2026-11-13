using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.CodeGeneration;

/// <summary>
/// The hook an option gives when its path is unusable (the empty string, or <c>-</c> which would mix code with the body): it refuses every transfer with exit 2, naming the
/// option, without contacting the server, rather than let the empty path reach the file system.
/// </summary>
/// <param name="optionName">The long name of the option, without its leading <c>--</c>.</param>
/// <param name="expectation">What the option expects of its path, completing the message after the option name.</param>
internal sealed class EmptyPathTransferHook(string optionName, string expectation) : ITransferHook
{
    /// <summary>Gets the message the refused transfer reports.</summary>
    public string Message { get; } = "--" + optionName + " " + expectation;

    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next) =>
        ValueTask.FromResult(TransferResult.Failure(CurlExitCode.FailedInit, Message));
}
