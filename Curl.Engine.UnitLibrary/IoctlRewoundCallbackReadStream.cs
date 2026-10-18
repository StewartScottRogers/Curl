using System.Globalization;
using Curl.Api.Callbacks;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// A <see cref="CallbackReadStream" /> whose handle set <c>CURLOPT_IOCTLFUNCTION</c> (<see cref="IoctlCallback" />)
/// and no <c>CURLOPT_SEEKFUNCTION</c>, so an upload a followed redirect or an authentication retry sends again is
/// rewound as curl 8.22.0's <c>cr_in_rewind</c> falls back to: only once the read callback has given some bytes,
/// by calling the ioctl callback with <see cref="IoctlCommand.RestartRead" />. Any result but
/// <see cref="IoctlResult.Ok" /> fails the transfer with exit 65 and <c>ioctl callback returned error &lt;n&gt;</c>
/// (BL-2607).
/// </summary>
/// <param name="callback">The read callback.</param>
/// <param name="ioctl">The ioctl callback.</param>
/// <param name="pauseGate">The handle's pause state, or <see langword="null" /> when the transfer cannot pause.</param>
internal sealed class IoctlRewoundCallbackReadStream(ReadCallback callback, IoctlCallback ioctl, EasyPauseGate? pauseGate = null)
    : CallbackReadStream(callback, pauseGate), IUploadRewinder
{
    /// <summary>The start of libcurl's message for an ioctl callback that did not return <see cref="IoctlResult.Ok" />.</summary>
    internal const string IoctlFailedPrefix = "ioctl callback returned error ";

    /// <inheritdoc />
    public bool HasBeenRead => BytesReadSinceRewind > 0;

    /// <inheritdoc />
    public string? RewindToStart()
    {
        IoctlResult result = ioctl(IoctlCommand.RestartRead);
        if (result != IoctlResult.Ok)
        {
            return IoctlFailedPrefix + ((int)result).ToString(CultureInfo.InvariantCulture);
        }

        BytesReadSinceRewind = 0;
        return null;
    }
}
