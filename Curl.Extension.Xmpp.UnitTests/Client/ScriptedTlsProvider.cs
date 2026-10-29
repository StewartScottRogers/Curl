using Curl.Protocol.Abstractions;

namespace Curl.Extension.Xmpp.Client;

/// <summary>A fake <see cref="ITlsProvider"/> that records the connection and host it secures and returns a set result.</summary>
internal sealed class ScriptedTlsProvider(ConnectResult result) : ITlsProvider
{
    public IConnection? Plaintext { get; private set; }

    public string? TargetHost { get; private set; }

    public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken)
    {
        Plaintext = plaintext;
        TargetHost = targetHost;
        return ValueTask.FromResult(result);
    }
}
