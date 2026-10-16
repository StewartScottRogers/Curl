# ADR-0588 — `Libcurl`'s `CurlMulti` socket-action API passes through, and its callbacks hand back Libcurl's handles

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2431.

## Context

Phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` lists `curl_multi_socket_action`,
`curl_multi_assign` and the `CURLMOPT_SOCKETFUNCTION`/`TIMERFUNCTION` callbacks. The seam already has
`IMultiTransfer.SocketActionAsync`, `Assign`, `CurlMultiSockets.Timeout`, `CurlPoll`, `CurlSocketSelect`
and `MultiOptions.Socket`/`Timer`, whose delegates are `MultiSocketCallback(IEasyTransfer, ...)` and
`MultiTimerCallback(long)`. A libcurl application, though, receives its own `CURL *` easy handle in the
socket callback and its `CURLM *` multi handle in the timer callback. `curl_multi_setopt` for every
`CURLMOPT_*` is BL-2432's `SetOption` in `Libcurl/Multi/Settings` (ADR-0582). The managed engine's
socket-action work is BL-2426.

## Decision

1. `Libcurl/Multi/Sockets` adds a `CurlMulti` part: `SocketActionAsync(CurlSocket, CurlSocketSelect, CancellationToken)`
   and `Assign(CurlSocket, object?)` call the engine's members as they are and answer its result and
   `running_handles` unchanged. A cleaned-up multi handle throws `ObjectDisposedException`, as
   `Multi/Core`'s members do.
2. Libcurl has its own delegates, `CurlMultiSocketCallback(CurlEasy, CurlSocket, CurlPoll, object?)` and
   `CurlMultiTimerCallback(CurlMulti, long)`, matching `curl_socket_callback` and
   `curl_multi_timer_callback`. `CURLMOPT_SOCKETDATA` and `TIMERDATA` are the delegates' closures, as
   every other Libcurl callback's user pointer is.
3. The internal `SetSocketFunction` and `SetTimerFunction` wrap them into the seam's delegates (the
   engine transfer mapped back to the `CurlEasy` the caller added, and this `CurlMulti` passed to the
   timer) and write `MultiOptions` with a `with` expression, so the other options are kept; `null`
   clears the callback. They are internal so that `curl_multi_setopt` stays one public `SetOption`,
   BL-2432's, which calls them for a `SOCKETFUNCTION` or `TIMERFUNCTION` value of Libcurl's own delegate
   type; the seam's own delegates still go through `MultiOptionSetters` unchanged.

## Consequences

- Libcurl callers never see the engine's `IEasyTransfer`; the socket callback names the handle they added.
- Applications set the two callbacks with `SetOption(CurlMultiOption.SocketFunction, ...)` and `TimerFunction`.
- The behaviour behind the calls (which sockets are reported, when the timer fires) is the engine's, BL-2426.
