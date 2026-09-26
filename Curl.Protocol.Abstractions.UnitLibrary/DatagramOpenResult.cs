namespace Curl.Protocol.Abstractions;

/// <summary>
/// The outcome of one
/// <see cref="IDatagramConnector.OpenAsync(string, int, CancellationToken)" />: an open
/// datagram channel, or the curl exit code and message that say why there is none.
/// </summary>
/// <remarks>
/// A result is built only through <see cref="Opened(IDatagramChannel)" /> and
/// <see cref="Failed(CurlExitCode, string)" />, so a success always carries a channel and
/// a failure always carries an exit code other than <see cref="CurlExitCode.Ok" />.
/// </remarks>
public sealed class DatagramOpenResult
{
    private DatagramOpenResult(IDatagramChannel? channel, CurlExitCode exitCode, string? errorMessage)
    {
        Channel = channel;
        ExitCode = exitCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Gets the open channel, non-<see langword="null" /> exactly when
    /// <see cref="ExitCode" /> is <see cref="CurlExitCode.Ok" />. Ownership passes to the
    /// caller, which disposes it.
    /// </summary>
    public IDatagramChannel? Channel { get; }

    /// <summary>
    /// Gets <see cref="CurlExitCode.Ok" /> for a success, or the curl exit code a failure
    /// carries, such as <see cref="CurlExitCode.CouldntResolveHost" /> (6) or
    /// <see cref="CurlExitCode.CouldntConnect" /> (7).
    /// </summary>
    public CurlExitCode ExitCode { get; }

    /// <summary>
    /// Gets the message curl prints for the failure, or <see langword="null" /> for a
    /// success.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Creates the result of a successful open.
    /// </summary>
    /// <param name="channel">The open channel.</param>
    /// <returns>
    /// A result whose <see cref="ExitCode" /> is <see cref="CurlExitCode.Ok" /> and whose
    /// <see cref="ErrorMessage" /> is <see langword="null" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="channel" /> is <see langword="null" />, which would leave a success
    /// with nothing to send or receive on.
    /// </exception>
    public static DatagramOpenResult Opened(IDatagramChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        return new DatagramOpenResult(channel, CurlExitCode.Ok, null);
    }

    /// <summary>
    /// Creates the result of a failed resolve or open.
    /// </summary>
    /// <param name="exitCode">
    /// The curl exit code, such as <see cref="CurlExitCode.CouldntResolveHost" /> (6) or
    /// <see cref="CurlExitCode.CouldntConnect" /> (7).
    /// </param>
    /// <param name="errorMessage">The message curl prints for the failure.</param>
    /// <returns>A result with no <see cref="Channel" />.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="exitCode" /> is <see cref="CurlExitCode.Ok" />, which is not a
    /// failure; use <see cref="Opened(IDatagramChannel)" /> instead.
    /// </exception>
    public static DatagramOpenResult Failed(CurlExitCode exitCode, string errorMessage)
    {
        if (exitCode == CurlExitCode.Ok)
        {
            throw new ArgumentOutOfRangeException(
                nameof(exitCode),
                exitCode,
                "A failed open cannot report CurlExitCode.Ok; use DatagramOpenResult.Opened instead.");
        }

        return new DatagramOpenResult(null, exitCode, errorMessage);
    }
}
