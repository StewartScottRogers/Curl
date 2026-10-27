# ADR-0863 — `curl_multi_perform` never calls the socket callback, and a multi handle a callback killed calls no callback again

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3677 (CGR-148; amends ADR-0599)
- Decided by Claude under Stewart's delegation.

## Context

ADR-0599 had `PerformAsync` tell `CURLMOPT_SOCKETFUNCTION` of each transfer's socket as
`SocketActionAsync` does, and a socket or timer callback's -1 end every unfinished transfer with
`CURLE_ABORTED_BY_CALLBACK` on the next call of either. The third-party gap report (CGR-148) found
that libcurl 8.22.0 does neither. Measured on Windows with Git for Windows' ucrt64
`libcurl-4.dll` 8.22.0 (Schannel) through a C# P/Invoke probe and a loopback server answering
`200 hello`, each mode driven for 2 s:

| Mode | Codes (code/running) | Messages | Socket callback | Timer callback |
|---|---|---|---|---|
| socket action every 100 ms, socket callback -1 on its first call | `11/1 0/1 0/1 ...` | none | 1 call | `0` (from add) only |
| perform + poll, socket callback set (answering -1) | `0/1 0/1 0/0` | `CURLMSG_DONE` 0 | never | `0`, `-1` |
| socket action, timer callback -1 on its second call | `11/1 0/1 0/1 ...` | none | 1 call | 2 calls |
| perform + poll, timer callback -1 on its second call | `11/1 0/0 0/0` | `CURLMSG_DONE` 42 | never | 2 calls |

## Decision

- `PerformAsync` never calls the socket callback; it still tells the timer callback.
- Once a callback has answered -1 the handle is dead: every later `SocketActionAsync` answers
  `CURLM_OK` and the transfers not finished, moving none on, queuing no message and calling neither
  callback; a later `PerformAsync` ends every unfinished transfer with `CURLE_ABORTED_BY_CALLBACK`
  (as before) and calls neither callback.
- A transfer's managed perform keeps running after the handle dies, as libcurl leaves the easy
  handle in place; its result is never reported by a socket action.

## Consequences

An application that drives the handle with `curl_multi_perform` and sets a socket callback no
longer sees socket calls, as with libcurl. The tests of ADR-0599 that drove the socket callback
through perform now drive it through socket actions. Tests: `Cgr148Tests`.
