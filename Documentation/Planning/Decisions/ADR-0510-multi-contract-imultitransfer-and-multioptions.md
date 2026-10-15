# ADR-0510 — The multi contract: `IMultiTransfer` and `MultiOptions`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2313.

## Context

ADR-0506 and ADR-0509 define the engine contracts for one easy handle. BL-2313 adds the multi
handle's contract in `Curl.Api.UnitLibrary/EngineContracts/Multi` (namespace
`Curl.Api.EngineContracts.Multi`), matching libcurl 8.22.0's `include/curl/multi.h`. A few
shapes had more than one reasonable answer.

## Decision

- The option record is `MultiOptions`, not `EasyMultiOptions` as BL-2313 first named it: the
  `Easy*Options` records are `curl_easy_setopt` groups, and these are `curl_multi_setopt`'s, so
  an `Easy` prefix would say something false. `IMultiTransfer.Options` is settable and changed
  with `with`, as `IEasyTransfer.Options` is. Defaults: `MAXCONNECTS`, `MAX_HOST_CONNECTIONS`
  and `MAX_TOTAL_CONNECTIONS` 0, `MAX_CONCURRENT_STREAMS` 100, `PIPELINING`
  `CURLPIPE_MULTIPLEX`, `NETWORK_CHANGED` none, every callback `null`.
- Each callback's `*DATA` user pointer is the delegate's closure, as for the easy callbacks.
  `CURLMOPT_NOTIFYFUNCTION` is in the record because `curl_multi_notify_enable` has nothing to
  call without it.
- Calls that return a code and a count (`curl_multi_perform`, `_wait`, `_poll`,
  `_socket_action`, `_timeout`, `_get_offt`) return a named tuple `(CurlMultiCode Code, ...)`
  rather than an `out` parameter, since the async ones cannot take one.
- `curl_multi_wait` and `_poll` take extra sockets as `Memory<CurlWaitSocket>`, so the wait can
  write each one's `revents` back as libcurl does; `curl_multi_waitfds` fills a
  `Span<CurlWaitSocket>` and reports the full count. It is named `GetWaitSockets`, and
  `curl_multi_get_offt` is `GetInfo(CurlMultiInfo)`, since "offt" names a C type, not what is
  read.
- `CURL_SOCKET_TIMEOUT` is `CurlMultiSockets.Timeout`, equal to `CurlSocket.Bad` as upstream
  defines it.
- The `CURLMSG_DONE` message is the record `CurlMultiMessage(Type, Transfer, Result)`, with
  `CurlMultiMessageType` carrying `CURLMSG_NONE` and `CURLMSG_DONE`.
- `curl_multi_fdset` gets no member: an `fd_set` means nothing to .NET. `Libcurl` answers it
  from `GetWaitSockets` in phase 3.

## Consequences

The engine's multi implementation (phase 3) implements `IMultiTransfer` and
`ICurlEngine.CreateMulti()`. Tests in `Curl.Api.UnitTests/EngineContracts/Multi` pin every
upstream number and `MultiOptions`' defaults.
