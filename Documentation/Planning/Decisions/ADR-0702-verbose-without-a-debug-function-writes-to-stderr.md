# ADR-0702 — A library handle's `CURLOPT_VERBOSE` with no debug function writes libcurl's default lines to its stderr

- **Status:** Accepted
- **Date:** 2026-10-19

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2726.

## Context

Upstream cases 1506 and 1510 (`lib1506`, `lib1510`) set `CURLOPT_VERBOSE` on each handle, set no
`CURLOPT_DEBUGFUNCTION`, and expect each transfer's `* Connection #N to host <host>:<port> left intact` line on the
program's stderr. libcurl 8.22.0's `Curl_debug` (`lib/curl_trace.c`) falls back, with no debug function, to writing
`* `, `< ` or `> ` and then the bytes of each info text, incoming header and outgoing header call to `CURLOPT_STDERR`,
which defaults to the process's `stderr`; data and TLS bytes are not written. Curl's engine gave a handle events only
when both verbose and a debug callback were set (ADR-0543), so these programs wrote nothing. The ratchet runs libtest
programs in process, so "the process's stderr" has to be the case's stderr there.

## Decision

- Under `EasyBehaviourOptions.Verbose` with no `EasyCallbackOptions.Debug`, `ManagedEasyTransfer` reports to a
  `DebugCallbackTransferEvents` over `StandardErrorDebugWriter`, which writes `Curl_debug`'s default output to
  `EasyBehaviourOptions.StandardError` (`CURLOPT_STDERR`), else the handle's `EasyTransferHost.StandardError`.
- `EasyTransferHost.StandardError` is the process's standard error (opened when first used) for every handle, unless
  `ManagedCurlEngine`'s internal constructor is given a `standardError`; `UpstreamConformanceTests.CreateEngine` passes
  the case's stderr, as it passes the case's stdin (ADR-0683).
- The curl tool is unchanged: its runner reports through `ToolEvents`, never through these events.

## Consequences

- 1506 and 1510 pass and hold the ratchet; any other libtest program that sets `CURLOPT_VERBOSE` without a debug
  function now writes libcurl's lines on its stderr, as upstream's does.
- A `Curl.Api` or `Libcurl` handle with `CURLOPT_VERBOSE` and no debug function writes to the process's standard error,
  as libcurl does; before, it wrote nothing.
