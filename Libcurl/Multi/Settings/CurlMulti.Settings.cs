using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;

namespace Libcurl.Multi;

/// <summary>
/// The settings area's part of <see cref="CurlMulti"/> (ADR-0582): <c>curl_multi_setopt</c>,
/// <c>curl_multi_get_handles</c>, <c>curl_multi_waitfds</c>, <c>curl_multi_get_offt</c> and
/// <c>curl_multi_notify_enable</c> and <c>_disable</c>.
/// </summary>
public sealed partial class CurlMulti
{
    /// <summary><c>curl_multi_setopt</c>: sets <paramref name="option"/> to <paramref name="value"/>.</summary>
    /// <param name="option">The option to set.</param>
    /// <param name="value">
    /// The value: a <see cref="long"/> or <see cref="int"/> for a <c>long</c> or <c>curl_off_t</c> option (or the
    /// option's enum, <see cref="CurlPipelining"/> or <see cref="CurlNetworkChange"/>), the option's own
    /// delegate type or <see langword="null"/> for a function, any value but a delegate for an object pointer.
    /// </param>
    /// <returns>
    /// <see cref="CurlMultiCode.Ok"/> once the option is set, <see cref="CurlMultiCode.UnknownOption"/> for an
    /// option <c>multi.h</c> lacks, else <see cref="CurlMultiCode.BadFunctionArgument"/> for a value of the
    /// wrong type or out of the option's range; the options are then unchanged.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The multi handle has been cleaned up.</exception>
    public CurlMultiCode SetOption(CurlMultiOption option, object? value)
    {
        var live = LiveTransfer();
        var options = live.Options;
        var code = MultiOptionSetters.SetOption(ref options, option, value);
        if (code == CurlMultiCode.Ok)
        {
            live.Options = options;
        }

        return code;
    }

    /// <summary><c>curl_multi_get_handles</c>: every easy handle added to this multi handle and not yet removed.</summary>
    /// <returns>
    /// The handles, in the engine's order; a transfer the engine holds that was not added through this
    /// handle (a server push's) is left out, as it has no <see cref="CurlEasy"/>.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The multi handle has been cleaned up.</exception>
    public IReadOnlyList<CurlEasy> GetHandles()
    {
        var easies = new List<CurlEasy>();
        foreach (var easyTransfer in LiveTransfer().GetHandles())
        {
            if (handles.TryGetValue(easyTransfer, out var easy))
            {
                easies.Add(easy);
            }
        }

        return easies;
    }

    /// <summary><c>curl_multi_waitfds</c>: the sockets and events the handle waits on.</summary>
    /// <param name="sockets"><c>ufds</c>: filled with up to its length of sockets; empty to ask only for the count.</param>
    /// <param name="socketCount"><c>fd_count</c>: how many sockets the handle waits on, even past <paramref name="sockets"/>' length.</param>
    /// <returns>The engine's answer; <see cref="CurlMultiCode.OutOfMemory"/> when <paramref name="sockets"/> is too short.</returns>
    /// <exception cref="ObjectDisposedException">The multi handle has been cleaned up.</exception>
    public CurlMultiCode WaitFds(Span<CurlWaitSocket> sockets, out int socketCount) =>
        LiveTransfer().GetWaitSockets(sockets, out socketCount);

    /// <summary><c>curl_multi_get_offt</c>: one of the handle's transfer counts.</summary>
    /// <param name="info">The count to read.</param>
    /// <param name="value">The count; -1 when <paramref name="info"/> is not one <c>multi.h</c> defines.</param>
    /// <returns>
    /// The engine's answer, or <see cref="CurlMultiCode.UnknownOption"/> without asking it for
    /// <see cref="CurlMultiInfo.None"/> or a number past <see cref="CurlMultiInfo.TransfersAdded"/>, as <c>multi.c</c> does.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The multi handle has been cleaned up.</exception>
    public CurlMultiCode GetInfo(CurlMultiInfo info, out long value)
    {
        var live = LiveTransfer();
        if (info is < CurlMultiInfo.TransfersCurrent or > CurlMultiInfo.TransfersAdded)
        {
            value = -1;
            return CurlMultiCode.UnknownOption;
        }

        (var code, value) = live.GetInfo(info);
        return code;
    }

    /// <summary><c>curl_multi_notify_enable</c>: sends <paramref name="notification"/> to <c>CURLMOPT_NOTIFYFUNCTION</c>.</summary>
    /// <param name="notification">The notification to switch on.</param>
    /// <returns>The engine's answer, or <see cref="CurlMultiCode.UnknownOption"/> without asking it for a notification <c>multi.h</c> lacks.</returns>
    /// <exception cref="ObjectDisposedException">The multi handle has been cleaned up.</exception>
    public CurlMultiCode NotifyEnable(CurlMultiNotification notification)
    {
        var live = LiveTransfer();
        return IsKnown(notification) ? live.EnableNotification(notification) : CurlMultiCode.UnknownOption;
    }

    /// <summary><c>curl_multi_notify_disable</c>: stops sending <paramref name="notification"/>.</summary>
    /// <param name="notification">The notification to switch off.</param>
    /// <returns>The engine's answer, or <see cref="CurlMultiCode.UnknownOption"/> without asking it for a notification <c>multi.h</c> lacks.</returns>
    /// <exception cref="ObjectDisposedException">The multi handle has been cleaned up.</exception>
    public CurlMultiCode NotifyDisable(CurlMultiNotification notification)
    {
        var live = LiveTransfer();
        return IsKnown(notification) ? live.DisableNotification(notification) : CurlMultiCode.UnknownOption;
    }

    private static bool IsKnown(CurlMultiNotification notification) =>
        notification is CurlMultiNotification.InfoRead or CurlMultiNotification.EasyDone;
}
