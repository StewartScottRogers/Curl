using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// The <see cref="IDatagramConnector"/> for a UDP port nothing listens on: every open fails with
/// <see cref="CurlExitCode.CouldntConnect"/>, and no socket is opened. <see cref="TftpServerConnector"/>
/// hands every open to a port other than its own to it.
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
