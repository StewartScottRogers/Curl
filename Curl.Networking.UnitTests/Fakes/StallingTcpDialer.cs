using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="ITcpDialer" /> whose every dial never completes, as a dial to a non-routable
/// address does, until its token is cancelled. <see cref="OnStalled" /> runs as each dial
/// starts waiting, so a test can move the clock at that moment.
/// </summary>
public sealed class StallingTcpDialer : ITcpDialer
{
    /// <summary>Gets or sets what runs as each dial starts waiting; nothing by default.</summary>
    public Action OnStalled { get; init; } = () => { };

    /// <inheritdoc />
    public async ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        OnStalled();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("An infinite delay ended without being cancelled.");
    }
}
