using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options.Http;
using Curl.Networking;

namespace Curl.Engine;

internal sealed partial class ManagedMultiTransfer
{
    /// <summary>
    /// <c>curl_multi_get_offt</c>: one of the handle's transfer counts, as curl 8.22.0 keeps them. Current is
    /// every transfer added and not removed; pending every one of those not started that a connection limit
    /// holds back; running every other one whose message is not yet queued, started or not; done every one
    /// whose message is queued; added every transfer ever added.
    /// </summary>
    /// <param name="info">The count to report.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.Ok" /> and the count; <see cref="CurlMultiCode.UnknownOption" /> and 0 for
    /// <see cref="CurlMultiInfo.None" /> or any other value, and <see cref="CurlMultiCode.BadHandle" /> and 0
    /// once this handle is disposed.
    /// </returns>
    public (CurlMultiCode Code, long Value) GetInfo(CurlMultiInfo info)
    {
        if (disposed)
        {
            return (CurlMultiCode.BadHandle, 0);
        }

        return info switch
        {
            CurlMultiInfo.TransfersCurrent => (CurlMultiCode.Ok, entries.Count),
            CurlMultiInfo.TransfersRunning => (CurlMultiCode.Ok, entries.Count(entry => !entry.Done && !IsPending(entry))),
            CurlMultiInfo.TransfersPending => (CurlMultiCode.Ok, entries.Count(IsPending)),
            CurlMultiInfo.TransfersDone => (CurlMultiCode.Ok, entries.Count(entry => entry.Done)),
            CurlMultiInfo.TransfersAdded => (CurlMultiCode.Ok, transfersAdded),
            _ => (CurlMultiCode.UnknownOption, 0),
        };
    }

    /// <summary>
    /// <c>curl_multi_notify_enable</c>: from the next perform or socket action on, sends
    /// <paramref name="notification" /> to <see cref="MultiOptions.Notify" />: <see cref="CurlMultiNotification.InfoRead" />
    /// when a message is queued with none queued before it, then <see cref="CurlMultiNotification.EasyDone" />
    /// for each transfer whose message is queued, each with that transfer, once the call has moved every
    /// transfer on and before it tells the socket callback (on a socket action) and the timer callback.
    /// </summary>
    /// <param name="notification">The notification to switch on; any number from 0 to 31 is kept.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.Ok" />; <see cref="CurlMultiCode.UnknownOption" /> for a number outside 0 to 31
    /// and <see cref="CurlMultiCode.BadHandle" /> once this handle is disposed.
    /// </returns>
    public CurlMultiCode EnableNotification(CurlMultiNotification notification) =>
        ChangeNotification(notification, enable: true);

    /// <summary><c>curl_multi_notify_disable</c>: stops sending <paramref name="notification" />.</summary>
    /// <param name="notification">The notification to switch off.</param>
    /// <returns>As <see cref="EnableNotification" />.</returns>
    public CurlMultiCode DisableNotification(CurlMultiNotification notification) =>
        ChangeNotification(notification, enable: false);

    /// <summary>
    /// A server push on <paramref name="parent" />, as libcurl's HTTP/2 push promise handling answers it:
    /// refused when the handle is disposed, <paramref name="parent" /> is not in it, no
    /// <see cref="MultiOptions.Push" /> is set or <paramref name="pushed" /> is in a multi handle already;
    /// otherwise the callback decides. <see cref="CurlPushResult.Ok" /> adds <paramref name="pushed" />,
    /// which starts on the next perform as an added transfer does; <see cref="CurlPushResult.ErrorOut" />
    /// refuses it and ends <paramref name="parent" /> with <see cref="CurlCode.Http2" /> on the next perform;
    /// any other answer refuses it, as libcurl treats every answer but <c>CURL_PUSH_OK</c>.
    /// </summary>
    /// <param name="parent">The transfer the server pushed on.</param>
    /// <param name="pushed">The new transfer for the pushed stream.</param>
    /// <param name="headers">The push promise's headers as <c>name: value</c> lines, in order.</param>
    /// <returns><see cref="CurlPushResult.Ok" /> when the push joined the handle, else what refused it.</returns>
    internal CurlPushResult OfferPush(IEasyTransfer parent, IEasyTransfer pushed, IReadOnlyList<string> headers)
    {
        MultiEntry? parentEntry = PushParentOf(parent, pushed);
        if (parentEntry is null || Options.Push is not { } callback)
        {
            return CurlPushResult.Deny;
        }

        CurlPushResult answer = callback(parent, pushed, headers);
        if (answer == CurlPushResult.ErrorOut)
        {
            parentEntry.ForcedResult = CurlCode.Http2;
            return answer;
        }

        if (answer != CurlPushResult.Ok)
        {
            return CurlPushResult.Deny;
        }

        // The timer's answer to the add is the handle's to report on its next call, not the push's.
        owners[pushed] = this;
        AddEntry(pushed);
        return CurlPushResult.Ok;
    }

    /// <summary>The parent's entry, or <see langword="null" /> when the handle is disposed, the parent is not in it, or the pushed transfer is null or in a multi handle already.</summary>
    private MultiEntry? PushParentOf(IEasyTransfer parent, IEasyTransfer pushed)
    {
        if (disposed || pushed is null || owners.ContainsKey(pushed))
        {
            return null;
        }

        return entries.Find(candidate => candidate.Transfer == parent);
    }

    private static bool IsNotificationNumber(CurlMultiNotification notification) => (uint)notification < 32;

    private CurlMultiCode ChangeNotification(CurlMultiNotification notification, bool enable)
    {
        if (disposed)
        {
            return CurlMultiCode.BadHandle;
        }

        if (!IsNotificationNumber(notification))
        {
            return CurlMultiCode.UnknownOption;
        }

        uint bit = 1u << (int)notification;
        enabledNotifications = enable ? enabledNotifications | bit : enabledNotifications & ~bit;
        return CurlMultiCode.Ok;
    }

    private bool IsEnabled(CurlMultiNotification notification) => (enabledNotifications & (1u << (int)notification)) != 0;

}
