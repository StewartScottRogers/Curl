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
    /// Gets a value indicating whether the transfer succeeded without delivering a body
    /// because its <c>-z</c>/<c>--time-cond</c> condition was not met; <see langword="false" />
    /// for every other result, a zero-byte download included. curl 8.21.0 creates no
    /// <c>-o</c> file for such a transfer and leaves an existing one's content untouched.
    /// </summary>
    public bool TimeConditionUnmet { get; init; }

    /// <summary>
    /// Gets what the transfer learned beyond <see cref="ExitCode" /> and
    /// <see cref="BytesTransferred" />, for <c>-w</c>/<c>--write-out</c> and
    /// <c>-L</c>/<c>--location</c>; <see langword="null" /> when the handler reported
    /// nothing more. A failed transfer may carry one too. A handler fills it with
    /// <c>with</c>: <c>TransferResult.Success(n) with { Report = report }</c>. See ADR-0015.
    /// </summary>
    public TransferReport? Report { get; init; }

    /// <summary>
    /// Gets a value indicating whether the transfer failed with exit 7 because the peer
    /// refused the connect (<see cref="ConnectResult.IsConnectionRefused" />), the only
    /// exit 7 curl's <c>--retry-connrefused</c> retries; <see langword="false" /> for every
    /// other result. A handler sets it with <c>with</c> from its failed
    /// <see cref="ConnectResult" />.
    /// </summary>
    public bool IsConnectionRefused { get; init; }

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
    /// Creates a successful result that delivered no body because the transfer's
    /// <see cref="ITransferContext.TimeCondition" /> was not met.
    /// </summary>
    /// <param name="sourceLastWriteTimeUtc">
    /// The modification time of the resource that was checked, or <see langword="null" />
    /// when it is unknown; <c>-R</c>/<c>--remote-time</c> still applies it.
    /// </param>
    /// <returns>
    /// A successful <see cref="TransferResult" /> with no bytes moved and
    /// <see cref="TimeConditionUnmet" /> set.
    /// </returns>
    public static TransferResult TimeConditionNotMet(DateTimeOffset? sourceLastWriteTimeUtc = null) =>
        new(CurlExitCode.Ok, 0, null, sourceLastWriteTimeUtc) { TimeConditionUnmet = true };

    /// <summary>
    /// Creates a failed result, whose <see cref="SourceLastWriteTimeUtc" /> is always
    /// <see langword="null" />.
    /// </summary>
    /// <remarks>
    /// A failure reports the bytes that reached the destination before it, not zero, as
    /// curl 8.21.0's <c>%{size_download}</c> and <c>%{size_upload}</c> do.
    /// </remarks>
    /// <param name="exitCode">The code to report.</param>
    /// <param name="errorMessage">A description of the failure.</param>
    /// <param name="bytesTransferred">
    /// The number of payload bytes that reached the destination before the failure; zero
    /// when nothing moved.
    /// </param>
    /// <returns>A failed <see cref="TransferResult" />.</returns>
    public static TransferResult Failure(
        CurlExitCode exitCode,
        string errorMessage,
        long bytesTransferred = 0) =>
        new(exitCode, bytesTransferred, errorMessage);
}
