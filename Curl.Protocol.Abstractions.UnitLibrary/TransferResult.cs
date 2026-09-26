namespace Curl.Protocol.Abstractions;

/// <summary>
/// The outcome of one transfer.
/// </summary>
/// <param name="ExitCode">The code the process reports for this transfer.</param>
/// <param name="BytesTransferred">The number of payload bytes moved.</param>
/// <param name="ErrorMessage">
/// A human-readable failure description, or <see langword="null" /> on success. This is
/// the text behind the <c>%{errormsg}</c> write-out variable.
/// </param>
/// <param name="SourceLastWriteTimeUtc">
/// The modification time of the resource that was read, for the caller to apply when
/// <c>-R</c>/<c>--remote-time</c> was asked for; <see langword="null" /> when it is
/// unknown or no source was opened. A handler cannot apply it itself because it does not
/// own the file behind <see cref="ITransferContext.Output" />; see ADR-0003.
/// </param>
public sealed record TransferResult(
    CurlExitCode ExitCode,
    long BytesTransferred,
    string? ErrorMessage = null,
    DateTimeOffset? SourceLastWriteTimeUtc = null)
{
    /// <summary>
    /// Gets a value indicating whether the transfer succeeded.
    /// </summary>
    public bool IsSuccess => ExitCode == CurlExitCode.Ok;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="bytesTransferred">The number of payload bytes moved.</param>
    /// <param name="sourceLastWriteTimeUtc">
    /// The modification time of the resource that was read, or <see langword="null" />
    /// when it is unknown or no source was opened.
    /// </param>
    /// <returns>A successful <see cref="TransferResult" />.</returns>
    public static TransferResult Success(
        long bytesTransferred,
        DateTimeOffset? sourceLastWriteTimeUtc = null) =>
        new(CurlExitCode.Ok, bytesTransferred, null, sourceLastWriteTimeUtc);

    /// <summary>
    /// Creates a failed result, whose <see cref="SourceLastWriteTimeUtc" /> is always
    /// <see langword="null" />.
    /// </summary>
    /// <param name="exitCode">The code to report.</param>
    /// <param name="errorMessage">A description of the failure.</param>
    /// <returns>A failed <see cref="TransferResult" />.</returns>
    public static TransferResult Failure(CurlExitCode exitCode, string errorMessage) =>
        new(exitCode, 0, errorMessage);
}
