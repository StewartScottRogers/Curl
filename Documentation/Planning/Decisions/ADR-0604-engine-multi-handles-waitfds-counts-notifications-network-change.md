# ADR-0604 — The engine multi's get_handles, waitfds, get_offt, notifications and network change

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2427.

## Context

ADR-0506 put `curl_multi_get_handles`, `curl_multi_waitfds`, `curl_multi_get_offt`,
`curl_multi_notify_enable`/`_disable` and `CURLMOPT_NETWORK_CHANGED` on the seam (`IMultiTransfer`'s
`GetHandles`, `GetWaitSockets`, `GetInfo`, `EnableNotification`, `DisableNotification` and
`MultiOptions.NetworkChanged`), and `ManagedMultiTransfer` threw `NotSupportedException` for the first
five (ADR-0594). The managed engine's transfers run on their own and its connections are no operating
system descriptors (ADR-0599). No upstream source or real curl was reachable from the lane, so the rules
below follow curl 8.22.0's `lib/multi.c` and `lib/multi_ntfy.c` as recalled, not a measurement.

## Decision

1. **get_handles** lists the transfers added and not removed, in the order added; a disposed handle
   lists none (libcurl answers a bad handle with no list).
2. **waitfds** reports one `CURL_WAIT_POLLIN` socket for each transfer whose perform has started and
   whose message is not queued, numbered as the socket callback is told of it (`FirstTransferSocket`
   upwards). A transfer's number is given once, by whichever asks first, so waitfds and the socket
   callback always agree. An array too short is filled as far as it goes, the count is every socket,
   and the answer `CURLM_OUT_OF_MEMORY`, as libcurl answers.
3. **get_offt** counts as curl 8.22.0 does: `XFERS_CURRENT` the transfers added and not removed,
   `XFERS_RUNNING` those whose message is not queued (started or not, as libcurl's `process` set holds
   a transfer from its add), `XFERS_DONE` those whose message is queued, `XFERS_ADDED` every add ever.
   `XFERS_PENDING` is 0: the managed engine starts every transfer on the next perform and holds none
   back for a connection limit (BL-2428 adds the limits and revisits it). `CURLMINFO_NONE` and any
   other number are `CURLM_UNKNOWN_OPTION`.
4. **Notifications** are a bit per number, 0 to 31; a number outside that is `CURLM_UNKNOWN_OPTION`.
   A perform or socket action queues `CURLMNOTIFY_INFO_READ` when it queues a message with none queued
   before it, then `CURLMNOTIFY_EASY_DONE` for each transfer whose message it queues, and sends them
   to `CURLMOPT_NOTIFYFUNCTION` once every transfer has been moved on, before the socket and timer
   callbacks, as libcurl dispatches its notifications at the end of the call. Each carries the
   transfer it is about; libcurl's `INFO_READ` names the transfer whose message was queued too.
5. **`CURLMOPT_NETWORK_CHANGED` is an action.** Setting `MultiOptions` with it closes the engine's
   idle connections (`ConnectionCache.CloseIdleAsync`, new) and empties its DNS cache, `--resolve`
   entries included (`DnsCache.Clear`, new), at once; connections in use go on and return to the
   cache as before. The value is kept with `NetworkChanged` set back to `None`, so a later `with` on
   `Options` clears nothing again. The engine's caches are engine-wide (ADR-0542), so the multi
   clears them for every handle of the engine, where libcurl's are the multi handle's own.
   `DisposeAsync` waits for the closes to finish.

## Consequences

`Libcurl`'s `CurlMulti` (BL-2432) can pass every one of these through. The `XFERS_PENDING` count and
the notification numbers outside the two defined are unmeasured; a later measurement against real curl
that differs amends this ADR.
