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
public sealed record TransferResult(
    CurlExitCode ExitCode,
    long BytesTransferred,
    string? ErrorMessage = null)
{
    /// <summary>
    /// Gets a value indicating whether the transfer succeeded.
    /// </summary>
    public bool IsSuccess => ExitCode == CurlExitCode.Ok;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="bytesTransferred">The number of payload bytes moved.</param>
    /// <returns>A successful <see cref="TransferResult" />.</returns>
    public static TransferResult Success(long bytesTransferred) =>
        new(CurlExitCode.Ok, bytesTransferred);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="exitCode">The code to report.</param>
    /// <param name="errorMessage">A description of the failure.</param>
    /// <returns>A failed <see cref="TransferResult" />.</returns>
    public static TransferResult Failure(CurlExitCode exitCode, string errorMessage) =>
        new(exitCode, 0, errorMessage);
}
