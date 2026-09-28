namespace Curl.Protocol.Abstractions;

/// <summary>
/// The outcome of one <see cref="IConnectionListener.ListenAsync(ListenTarget, CancellationToken)" />:
/// a listening port, or the curl exit code and message that say why there is none (ADR-0102).
/// </summary>
/// <remarks>
/// A result is built only through <see cref="Listening(IPendingConnection)" /> and
/// <see cref="Failed(CurlExitCode, string)" />, so a success always carries a pending
/// connection and a failure always carries an exit code other than <see cref="CurlExitCode.Ok" />.
/// </remarks>
public sealed class ListenResult
{
    private ListenResult(IPendingConnection? pendingConnection, CurlExitCode exitCode, string? errorMessage)
    {
        PendingConnection = pendingConnection;
        ExitCode = exitCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Gets the listening port, non-<see langword="null" /> exactly when
    /// <see cref="ExitCode" /> is <see cref="CurlExitCode.Ok" />. Ownership passes to the
    /// caller, which disposes it.
    /// </summary>
    public IPendingConnection? PendingConnection { get; }

    /// <summary>
    /// Gets <see cref="CurlExitCode.Ok" /> for a success, or the curl exit code a failure
    /// carries.
    /// </summary>
    public CurlExitCode ExitCode { get; }

    /// <summary>
    /// Gets the message curl prints for the failure, or <see langword="null" /> for a
    /// success.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Creates the result of a successful bind.
    /// </summary>
    /// <param name="pendingConnection">The bound, listening port.</param>
    /// <returns>A result whose <see cref="ExitCode" /> is <see cref="CurlExitCode.Ok" />.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="pendingConnection" /> is <see langword="null" />.
    /// </exception>
    public static ListenResult Listening(IPendingConnection pendingConnection)
    {
        ArgumentNullException.ThrowIfNull(pendingConnection);

        return new ListenResult(pendingConnection, CurlExitCode.Ok, null);
    }

    /// <summary>
    /// Creates the result of a failed bind.
    /// </summary>
    /// <param name="exitCode">The curl exit code.</param>
    /// <param name="errorMessage">The message curl prints for the failure.</param>
    /// <returns>A result with no <see cref="PendingConnection" />.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="exitCode" /> is <see cref="CurlExitCode.Ok" />, which is not a
    /// failure; use <see cref="Listening(IPendingConnection)" /> instead.
    /// </exception>
    public static ListenResult Failed(CurlExitCode exitCode, string errorMessage)
    {
        if (exitCode == CurlExitCode.Ok)
        {
            throw new ArgumentOutOfRangeException(
                nameof(exitCode),
                exitCode,
                "A failed listen cannot report CurlExitCode.Ok; use ListenResult.Listening instead.");
        }

        return new ListenResult(null, exitCode, errorMessage);
    }
}
