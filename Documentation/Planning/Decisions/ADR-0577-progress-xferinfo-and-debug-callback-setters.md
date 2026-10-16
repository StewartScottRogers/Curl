# ADR-0577 — Progress, xferinfo and debug callbacks: Libcurl's setters over the engine's calls

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2416.

## Context

BL-2416 asks for `CURLOPT_PROGRESSFUNCTION`, `XFERINFOFUNCTION` and `DEBUGFUNCTION` to be called by the
engine with upstream's arguments and frequency, a non-zero progress return aborting with
`AbortedByCallback`. The engine side already landed under BL-2360 (ADR-0543): `CallbackTransferProgress`
calls the xferinfo function, else the progress function with the counts as doubles, on every progress
report unless `NoProgress` is set, and cancels the attempt on any return but 0 or
`CURL_PROGRESSFUNC_CONTINUE`; `DebugCallbackTransferEvents` hands every event to the debug function under
`Verbose` with its `CurlInfoType`. `CallbackTransferProgressTests`, `DebugCallbackTransferEventsTests` and
`ManagedEasyTransferTests` already pin the arguments and the abort path. What `Libcurl` lacked was the
`SetOption` rows, so those options still answered `NotBuiltIn`.

## Decision

1. The engine is left as it is. Its progress cadence is per progress report (every chunk received or
   sent, and the transfer's start and end), which is at least as often as libcurl's `pgrsupdate`
   (every chunk, and once a second while idle); the per-second idle tick is not added, since no caller
   can tell it apart from a report and nothing on the ratchet depends on it.
2. `Libcurl/Callbacks/Progress` implements `AddProgressCallbackSetters` with
   `EasyProgressCallbackSetters`: each function row takes its own delegate type or `null` (libcurl's
   default) and answers `BadFunctionArgument` for any other value, a `TransferInfoCallback` given to
   `PROGRESSFUNCTION` included, as the delegate types differ.
3. `CURLOPT_XFERINFODATA` (which `PROGRESSDATA` aliases, as in `curl.h`) and `CURLOPT_DEBUGDATA` take
   any value and change nothing: the managed callback carries its user data in its closure, and
   libcurl's default progress and debug functions read no `*DATA`.

## Consequences

- A Libcurl caller's progress, xferinfo and debug functions reach the engine through `SetOption`.
- The idle once-a-second progress call is a known difference; a task can add it if a caller needs it.
