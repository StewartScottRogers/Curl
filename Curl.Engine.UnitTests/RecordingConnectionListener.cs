using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// An <see cref="IConnectionListener" /> that records every target it is asked to bind and refuses each
/// with <see cref="CurlExitCode.FtpPortFailed" />, so no socket is ever opened.
/// </summary>
internal sealed class RecordingConnectionListener : IConnectionListener
{
    /// <summary>Gets every target asked for, in order.</summary>
    public List<ListenTarget> Targets { get; } = [];

    /// <inheritdoc />
    public ValueTask<ListenResult> ListenAsync(ListenTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        return ValueTask.FromResult(ListenResult.Failed(CurlExitCode.FtpPortFailed, "refused by the test"));
    }
}
