# ADR-0850 — Multi and easy calls from inside a transfer callback are refused

- **Status:** Accepted
- **Date:** 2026-10-26

Decided by Claude under Stewart's delegation (task BL-3675, CGR-146).

## Context

CGR-146 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`) found that Curl let an application call
the multi API and `curl_easy_perform` from inside a transfer's callback: each call took effect, so a
handle added there ran a nested transfer and `curl_easy_perform` of the handle whose callback ran
performed it again. libcurl refuses those calls with its `Curl_is_in_callback` check.

Measured on libcurl 8.22.0 (Schannel, Windows; Git for Windows' `ucrt64/bin/libcurl-4.dll`) with a
P/Invoke probe and a recording loopback server. From e1's first write callback, e1 being in multi `m`:
`curl_multi_perform`, `curl_multi_add_handle` of a new handle and of e1 itself (already in `m`),
`curl_multi_remove_handle(e1)`, `curl_multi_wait`, `curl_multi_poll`,
`curl_multi_socket_action(CURL_SOCKET_TIMEOUT)` and `curl_multi_timeout` all answer 8
(`CURLM_RECURSIVE_API_CALL`), `curl_multi_info_read` answers NULL, `curl_multi_wakeup` 0, and
`curl_easy_perform` of an unrelated handle 0; the only message is e1's, and the server sees
`GET /one` and `GET /three`. From an easy handle's own write callback, `curl_easy_perform` of itself
answers 93 (`CURLE_RECURSIVE_API_CALL`) and of another handle 0. The report measured 2 for the
self-perform on Linux's curl 8.18.0, an older release that checks `data->multi` first.

## Decision

- `ManagedMultiTransfer`'s `Add`, `Remove`, `PerformAsync`, `SocketActionAsync`, `WaitAsync`,
  `PollAsync` and `GetTimeout` answer `CurlMultiCode.RecursiveApiCall` and do nothing while
  `IsInTransferCallback` holds (code running inside one of the handle's transfers, its callbacks
  included), after the disposed and null checks; `Add` refuses before its added-already check, as
  8.22.0 answers 8 for a handle it holds. `TryReadInfo` answers no message there; `Wakeup` still works.
  The engine's own add of an HTTP/2 pushed transfer, which runs inside the parent's transfer, skips
  the check.
- `Libcurl`'s `CurlMulti.AddHandle` asks `IsInTransferCallback` before its own added-already check, so
  the answer matches through Libcurl too.
- `ManagedEasyTransfer.PerformAsync` answers `CurlCode.RecursiveApiCall` while a perform of the same
  handle runs (`ConnectOnlyConnection.PerformRunning`, BL-2654's stand-in for "a callback is
  running"), on every platform: Curl matches curl 8.22.0, the release it targets, not Linux's older
  8.18.0.

## Consequences

A callback that drove the multi handle it runs in, or re-performed its own handle, now gets libcurl's
refusal and the nested transfer never starts. Tests: `Curl.Engine.UnitTests`' `Cgr146Tests` and
`Libcurl.UnitTests`' `Cgr146Tests`.
