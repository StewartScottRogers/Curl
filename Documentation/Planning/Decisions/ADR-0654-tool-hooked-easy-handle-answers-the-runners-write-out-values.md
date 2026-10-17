# ADR-0654 — A tool-hooked easy handle answers the runner's `-w` values

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2495.

## Context

ADR-0625 (BL-2452) has `-w` read a serial transfer's libcurl values through its easy handle's `curl_easy_getinfo`
answers, but the runner still laid its own scheme, `Referer`, `xfer_id`, `conn_id`, both verify results and the early
data over them, and its normalized request URL over the effective URL when the report named none. The engine
transfer answered those from its own numbering (`ManagedEngineSharedState`'s `TransferPerformer`), from `CURLOPT_URL`,
and from a report the runner's events never filled with verify results. `Curl.Engine` cannot build the request URL
itself: the `-G` / `--url-query` query is `Curl.Cli`'s `QueryUrl`, which the engine never references.

## Decision

1. The runner's running transfer carries what the handle answers with: `EngineTransferState` gains `TransferId` (the
   transfer's `xfer_id`) and `RequestUrl` (the transferred URL with its query, normalized), which the runner sets once
   the URL is final (after the HSTS switch), and the verify results and early data move down to it from
   `RunningTransferState`, where the runner's `TlsResultRecordingTransferEvents` still records them.
2. `EngineToolTransferEvents` gains `TakeConnectionId`, the runner's own numbering (`state.ConnectionId ??=` its
   performer's next).
3. Under `ToolEvents` a perform from the options answers: `xfer_id` as the state's retry ID, else its `TransferId`;
   `conn_id` as -1 for an unsupported scheme or malformed URL, else the retried attempt's when the attempt reused that
   connection (`KeepRetriedConnectionIdWhenReused`, now done by the engine before it fills `Info`), else the state's,
   else `TakeConnectionId()` - kept on the state, so the runner's later `TakeConnectionId(result)` reads the same
   number and takes no other; the effective URL from the report else `RequestUrl`; the scheme of `RequestUrl`
   (`TransferInfoFromReport.RequestSchemeOf`, none after an unsupported scheme); the report's `Referer` else
   `CURLOPT_REFERER`; and the state's verify results and early data.
4. `EasyHandleWriteOutInfo.Read` reads those seven answers too, and `WriteOutVariables` takes the handle's info as it
   is when it describes the result. `-Z` transfers and transfers that never reached a handle keep
   `WriteOutTransferInfo.FromReport` with the runner's values (`ReportWriteOutInfo`) until BL-2453 reads `-Z` through
   its handles.

## Consequences

- No libcurl field is laid over a serial transfer's handle answers; every `-w` variable's output is unchanged, pinned
  by the existing write-out tests and the conformance ratchet.
- The engine takes a transfer's `conn_id` when its perform ends rather than the runner at `-w` time; serially nothing
  numbers a connection in between, and under `-Z` both happen in completion order.
- A handle performed without `ToolEvents` numbers and answers as before.
