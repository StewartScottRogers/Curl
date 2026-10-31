namespace Curl.Extension.ObliviousHttp.Relay;

/// <summary>Ends an Oblivious HTTP transfer early with the failed result it reports.</summary>
/// <param name="result">The failed transfer result, its exit code and error message.</param>
internal sealed class ObliviousTransferException(TransferResult result) : Exception(result.ErrorMessage)
{
    /// <summary>Gets the failed transfer result to report.</summary>
    public TransferResult Result { get; } = result;

    /// <summary>Gives the exception for a transfer that fails with <paramref name="exitCode" /> and <paramref name="message" />.</summary>
    public static ObliviousTransferException Failing(CurlExitCode exitCode, string message) => new(TransferResult.Failure(exitCode, message));
}
