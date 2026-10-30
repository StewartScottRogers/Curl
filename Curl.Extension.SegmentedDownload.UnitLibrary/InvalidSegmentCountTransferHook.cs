using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.SegmentedDownload;

/// <summary>
/// The hook <c>--segments</c> gives when its value is not a whole number from 1 to
/// <see cref="SegmentedDownloadExtension.MaximumSegments" />: it refuses every transfer with exit 2, as curl
/// refuses an option whose number it cannot read, without contacting the server (ADR-1087).
/// </summary>
internal sealed class InvalidSegmentCountTransferHook : ITransferHook
{
    /// <summary>The message the refused transfer reports.</summary>
    public const string Message = "--segments expects a whole number from 1 to 64";

    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next) =>
        ValueTask.FromResult(TransferResult.Failure(CurlExitCode.FailedInit, Message));
}
