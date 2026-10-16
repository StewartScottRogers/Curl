# ADR-0582 — Libcurl's curl_multi_setopt, get_handles, waitfds, get_offt and notify

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2432.

## Context

BL-2432 asks `CurlMulti` for `curl_multi_get_handles`, `_waitfds`, `_get_offt`, `_notify_enable` and
`_disable`, and `curl_multi_setopt` for every `CURLMOPT_*`. The seam (`IMultiTransfer`, ADR-0510) already
has `GetHandles`, `GetWaitSockets`, `GetInfo`, `EnableNotification`, `DisableNotification` and a
`MultiOptions` record holding every option that changes behaviour; what was missing was the Libcurl side.
No libcurl is installed on the lane machine to call `curl_multi_setopt` directly, so the checks below are
taken from curl 8.22.0's `lib/multi.c` as read, not measured.

## Decision

1. `CurlMultiOption` lists the 19 `CURLMOPT_*` of `multi.h` with upstream's numbers (type base plus
   number). `CurlMulti.SetOption(option, value)` goes through `MultiOptionSetters`, writing the changed
   `MultiOptions` back to the engine only on `Ok`, as `CurlEasy.SetOption` does. An option `multi.h` lacks
   answers `UnknownOption`; a value of the wrong .NET type (by the option's type base, with the easy
   side's `CurlOptionValues` rules) `BadFunctionArgument`.
2. Checks as `multi.c`: `MAXCONNECTS` takes 0 to `UINT_MAX`, `MAX_HOST_CONNECTIONS` and
   `MAX_TOTAL_CONNECTIONS` any count 0 or more, each refusing the rest with `BadFunctionArgument`;
   `MAX_CONCURRENT_STREAMS` keeps 1 to `INT_MAX` and turns any other value into 100; `PIPELINING` and
   `NETWORK_CHANGED` keep their bitmask as given (the engine reads the bits it knows).
3. The four functions take the seam's own delegate (`MultiSocketCallback`, `MultiTimerCallback`,
   `MultiPushCallback`, `MultiNotifyCallback`) or `null`, any other delegate `BadFunctionArgument`, as the
   easy callback setters do (ADR-0573). The callbacks therefore see `IEasyTransfer`, not `CurlEasy`;
   mapping them to the caller's `CurlEasy` belongs with the socket-action area (BL-2431).
4. The four `*DATA` options take any value and change nothing (the delegate's closure carries the user
   data), and the five options libcurl ignores since 7.62.0 (`MAX_PIPELINE_LENGTH`, both penalty sizes,
   both pipelining blocklists) are taken and ignored, answering `Ok`.
5. `GetInfo(CurlMultiInfo, out long)` is `curl_multi_get_offt`: `None` or a number past `XFERS_ADDED`
   answers `UnknownOption` with -1 without asking the engine, as `multi.c`'s `default:` does.
   `NotifyEnable` and `NotifyDisable` answer `UnknownOption` for a notification `multi.h` lacks, else the
   engine's code. `WaitFds` hands the span to `GetWaitSockets` as it is.
6. `GetHandles` answers the engine's transfers mapped to the `CurlEasy` each was added as, leaving out any
   transfer not added through this handle (a server push's), which has no `CurlEasy`.

## Consequences

- Every `CURLMOPT_*` reaches the engine's `MultiOptions` through `SetOption`.
- A pushed transfer is not in `GetHandles` until the push area gives it a `CurlEasy`.
