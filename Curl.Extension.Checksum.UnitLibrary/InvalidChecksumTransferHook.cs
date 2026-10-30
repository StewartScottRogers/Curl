using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Checksum;

/// <summary>
/// The hook <c>--checksum</c> gives when its value is not <c>&lt;alg&gt;:&lt;hex&gt;</c> with a known algorithm and
/// a digest of its length: it refuses every transfer with exit 2, as curl refuses an option value it cannot read,
/// without contacting the server (ADR-1092).
/// </summary>
internal sealed class InvalidChecksumTransferHook : ITransferHook
{
    /// <summary>The message the refused transfer reports.</summary>
    public const string Message = "--checksum expects <alg>:<hex>: sha1, sha256, sha384 or sha512 and a digest of its length";

    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next) =>
        ValueTask.FromResult(TransferResult.Failure(CurlExitCode.FailedInit, Message));
}
