using System.Globalization;
using Curl.Api.Callbacks;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// A <see cref="CallbackReadStream" /> whose handle also set <c>CURLOPT_SEEKFUNCTION</c>
/// (<see cref="SeekCallback" />), so an upload a followed redirect or an authentication retry sends again is
/// rewound through it, as curl 8.22.0's <c>cr_in_rewind</c> rewinds a read callback: only once the read
/// callback has given some bytes (its <c>read_len</c>), by calling the seek callback with offset 0 from
/// <see cref="SeekOrigin.Begin" />. Any result but <see cref="SeekResult.Ok" /> - <see cref="SeekResult.Fail" />
/// or <see cref="SeekResult.CantSeek" /> alike, whose read-and-discard workaround is only for
/// <c>CURLOPT_RESUME_FROM</c>'s forward seek - fails the transfer with exit 65 and
/// <c>seek callback returned error &lt;n&gt;</c> (BL-2417, ADR-0609).
/// </summary>
/// <param name="callback">The read callback.</param>
/// <param name="seek">The seek callback.</param>
/// <param name="pauseGate">The handle's pause state, or <see langword="null" /> when the transfer cannot pause.</param>
internal sealed class SeekingCallbackReadStream(ReadCallback callback, SeekCallback seek, EasyPauseGate? pauseGate = null)
    : CallbackReadStream(callback, pauseGate), IUploadRewinder
{
    /// <summary>The start of libcurl's message for a seek callback that did not return <see cref="SeekResult.Ok" />.</summary>
    internal const string SeekFailedPrefix = "seek callback returned error ";

    /// <inheritdoc />
    public bool HasBeenRead => BytesReadSinceRewind > 0;

    /// <inheritdoc />
    public string? RewindToStart()
    {
        SeekResult result = seek(0, SeekOrigin.Begin);
        if (result != SeekResult.Ok)
        {
            return SeekFailedPrefix + ((int)result).ToString(CultureInfo.InvariantCulture);
        }

        BytesReadSinceRewind = 0;
        return null;
    }
}
