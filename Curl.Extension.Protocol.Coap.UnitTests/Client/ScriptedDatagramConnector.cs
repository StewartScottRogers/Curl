using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Coap.Client;

/// <summary>A fake datagram connector that gives one open result and records the host and port it was asked for.</summary>
/// <param name="result">The result every open gives.</param>
internal sealed class ScriptedDatagramConnector(DatagramOpenResult result) : IDatagramConnector
{
    /// <summary>Gets the host and port of each open, in order.</summary>
    public List<(string Host, int Port)> Opened { get; } = [];

    /// <inheritdoc />
    public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        Opened.Add((host, port));
        return ValueTask.FromResult(result);
    }
}
