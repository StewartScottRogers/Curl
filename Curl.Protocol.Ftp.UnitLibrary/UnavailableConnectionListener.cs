using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// The <see cref="IConnectionListener" /> of an <see cref="FtpProtocolHandler" /> built
/// with a connector only: it binds nothing, so <c>-P</c> fails with exit 30 and
/// <c>Failed to do PORT</c>, curl's words for an active mode it cannot set up.
/// </summary>
internal sealed class UnavailableConnectionListener : IConnectionListener
{
    /// <summary>The one instance.</summary>
    public static readonly UnavailableConnectionListener Instance = new();

    private UnavailableConnectionListener()
    {
    }

    /// <inheritdoc />
    public ValueTask<ListenResult> ListenAsync(ListenTarget target, CancellationToken cancellationToken) =>
        ValueTask.FromResult(ListenResult.Failed(CurlExitCode.FtpPortFailed, FtpTransferMessages.FailedToDoPort));
}
