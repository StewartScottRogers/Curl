using System.Security.Cryptography;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Checksum;

/// <summary>
/// The hook <c>--checksum</c> gives (ADR-1092): it hashes the response body on its way to curl's output and, when
/// the transfer otherwise succeeded, fails it with exit 61 (<see cref="CurlExitCode.BadContentEncoding" />) unless
/// the digest is the one expected. The body is written as it arrives, as curl leaves a failed transfer's output.
/// </summary>
/// <param name="expected">The digest the body must have.</param>
internal sealed class ChecksumTransferHook(ExpectedChecksum expected) : ITransferHook
{
    /// <summary>Gets the digest the body must have.</summary>
    public ExpectedChecksum Expected { get; } = expected;

    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        using var hash = IncrementalHash.CreateHash(Expected.Algorithm);
        await using var hashing = new HashingStream(context.Output, hash);
        var result = await next(context.WithOutputs(hashing, context.HeaderOutput, context.DumpHeaderOutput)).ConfigureAwait(false);
        var digest = hash.GetHashAndReset();
        return !result.IsSuccess || CryptographicOperations.FixedTimeEquals(digest, Expected.Digest)
            ? result
            : result with { ExitCode = CurlExitCode.BadContentEncoding, ErrorMessage = $"checksum mismatch: expected {Expected.Format(Expected.Digest)}, got {Expected.Format(digest)}" };
    }
}
