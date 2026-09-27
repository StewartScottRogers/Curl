using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// The <see cref="IDatagramConnector"/> the harness hands curl until a TFTP emulation exists:
/// every open fails with <see cref="CurlExitCode.CouldntConnect"/>, and no socket is opened.
/// Cases that need the <c>tftp</c> server are skipped before they run, so only a case that
/// reaches for UDP unexpectedly sees it.
/// </summary>
public sealed class UnreachableDatagramConnector : IDatagramConnector
{
    /// <summary>The message the failed open carries.</summary>
    public const string Message = "The conformance harness emulates no UDP server";

    /// <summary>Fails at once.</summary>
    /// <param name="host">Ignored.</param>
    /// <param name="port">Ignored.</param>
    /// <param name="cancellationToken">Not observed.</param>
    /// <returns>A failed result with <see cref="CurlExitCode.CouldntConnect"/>.</returns>
    public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
        ValueTask.FromResult(DatagramOpenResult.Failed(CurlExitCode.CouldntConnect, Message));
}
