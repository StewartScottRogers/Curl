using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.WebDavTools.Transfer;

/// <summary>
/// The hook the options give when they cannot be used as given: it refuses every transfer with exit 2, as curl refuses
/// options it cannot use together, without contacting the server (ADR-1164).
/// </summary>
/// <param name="message">The message the refused transfer reports.</param>
internal sealed class RefusedWebDavTransferHook(string message) : ITransferHook
{
    /// <summary>Gets the message the refused transfer reports.</summary>
    public string Message => message;

    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next) =>
        ValueTask.FromResult(TransferResult.Failure(CurlExitCode.FailedInit, message));
}
