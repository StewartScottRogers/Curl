using Curl.Protocol.Abstractions;

namespace Curl.Extension.EncryptedDns.DnsOverTls;

/// <summary>
/// A connector that records each target and answers with <see cref="Plaintext" />, or fails with exit 7 when it is
/// <see langword="null" />.
/// </summary>
internal sealed class FakeConnector : IConnector
{
    public ScriptedConnection? Plaintext { get; init; } = new(isSecure: false);

    public List<ConnectTarget> Targets { get; } = [];

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        return ValueTask.FromResult(Plaintext is null
            ? ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect")
            : ConnectResult.Connected(Plaintext));
    }
}
