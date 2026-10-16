# ADR-0547 — The easy handle's info holds the live transfer and connection IDs

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2364.

## Context

ADR-0543 kept `DebugCallback`'s libcurl signature, which carries no transfer or connection ID. curl's
tool writes `--trace-ids`' `[<xfer>-<conn>] ` prefix by calling `curl_easy_getinfo` for
`CURLINFO_XFER_ID` and `CURLINFO_CONN_ID` inside its debug callback (`src/tool_cb_dbg.c`), and libcurl
answers with the running transfer's values, `-1` for the connection before there is one. Until now
`ManagedEasyTransfer.Info` was set only when `PerformAsync` ended, so a callback reading it saw the
previous transfer's values.

## Decision

- **`Info` is updated while the transfer performs**, rather than a new callback signature or a side
  channel: it is what `curl_easy_getinfo` reads, so the debug callback reads the handle as curl's tool
  does and `Curl.Api` gains no member.
- **At the start of a performed transfer**, once its `%{xfer_id}` is taken, `Info` becomes a fresh
  `TransferInfo` with the effective URL, `TransferId` and `ConnectionId` -1 (`NoConnectionId`, which the
  prefix writes as `x`), as libcurl's `Curl_initinfo` clears the previous transfer's info. A perform that
  fails before that (no URL, a malformed one) sets `Info` only when it finishes, as before.
- **`EngineConnectionIdTransferEvents` tells the handle each `%{conn_id}` it takes** - the new one when
  the transfer opens a connection, the opener's (or the pool's number) when it reuses one - through a
  `connectionIdTaken` action, *before* passing the event on, so the debug callback's `Connected to` line
  already reads the connection's number. A second connection (the happy-eyeballs loser) tells nothing.
- `FinishPerformed` still replaces `Info` whole when the transfer ends, so the finished values are
  unchanged.

## Consequences

- `ManagedEasyTransferTests.PerformAsync_DebugCallbackReadsTheHandlesIds_GetsNoConnectionBeforeItOpensAndItsNumberAfter`
  reads `easy.Info` inside the debug callback and finds `-1` before the connection opens and `0` from
  its `Connected to` line on, with transfer ID 0 throughout.
- `Curl.Console` can write the `--trace-ids` prefix from the handle once its runner performs through the
  engine (BL-2318).
