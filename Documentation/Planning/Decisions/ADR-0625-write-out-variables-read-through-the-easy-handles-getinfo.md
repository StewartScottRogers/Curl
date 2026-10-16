# ADR-0625 — `-w` variables read through the easy handle's `curl_easy_getinfo`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2452.

## Context

Phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` has the tool's `-w` variables read through
`CurlEasy.GetInfo`, as curl's `tool_writeout.c` reads them with `curl_easy_getinfo`. Since ADR-0624 a serial
transfer runs through a `CurlEasy`, but the runner built each transfer's `TransferInfo` itself from the result's
report (`WriteOutTransferInfo.FromReport`). Measured against the engine as it is now:

1. The handle is cleaned up (`curl_easy_cleanup`) when its attempts end, before the transfer's report and `-w`.
2. The engine transfer's `Info` fills the `-w` fields from the same report the same way (`TransferInfoFromReport`
   starts from `WriteOutTransferInfo`), but not every value: its `xfer_id` and `conn_id` come from the engine's own
   numbering, not the runner's `TransferPerformer`; its effective URL falls back to the handle's `CURLOPT_URL`, not the
   tool's normalized request URL; and the scheme, `Referer`, verify results and early data the runner prints are the
   tool's own answers (BL-2319).
3. Some transfers never reach a handle (a bad glob, an IPFS refusal, a `-T` file that cannot be opened, a skipped
   transfer) and `-Z` transfers perform without one until the multi handle lands (BL-2453).

## Decision

1. With `-w`, a serial transfer reads its handle's answers through `CurlEasy.GetInfo` after its last attempt and
   before cleanup (`EasyHandleWriteOutInfo.Read`), keeping them on its `RunningTransferState`, with the report of the
   perform they describe.
2. The `-w` values take those answers whenever the reported result carries that very report (a result the runner
   rewrote afterwards, such as a write failure's exit code, still does), with the tool's request URL as the effective
   URL when the report names none. Otherwise, for `-Z` and for a transfer that never reached its handle, they come from
   `WriteOutTransferInfo.FromReport`, as before.
3. The runner still lays its own answers over them: the scheme, `Referer`, `xfer_id`, `conn_id`, both verify results
   and the early data. BL-2495 moves those into the engine transfer's `Info` so the overlay can go.

## Consequences

- Every `-w` variable's output is unchanged, pinned by the existing write-out tests and the conformance ratchet.
- The libcurl part of a serial transfer's `-w` output (codes, method, URLs, sizes, counts, end points, times and
  speeds) now goes through `Libcurl`'s `EasyInfoDispatch`, so a later change to a `CURLINFO_*` reader shows in `-w`.
