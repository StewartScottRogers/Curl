# ADR-0545 — The easy handle's trace components wrap its debug events

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2363.

## Context

ADR-0543 gave `ManagedEasyTransfer`'s debug callback every event the handlers and connectors
report, but not the `[READ]`, `[WRITE]` and `[TIMER]` lines `CurlCommandRunner.WithTraceLineEvents`
adds under `--trace-config read`, `write` and `timer` with `ClientReaderResetTraceEvents`,
`ClientWriterTraceEvents` and `ResponseWaitTimerTraceEvents`. The runner takes the components from
`EngineTraceComponents`, which libcurl takes from `curl_global_trace`, a global setting rather than
an easy option, so `EasyOptions` has no place for it.

## Decision

- **An internal `ManagedEasyTransfer.TraceComponents` hook**, an `EngineTraceComponents`, beside
  `ToolSettings` (ADR-0542) and `Host` (ADR-0544), rather than an engine-wide setting: the hook is
  what `Curl.Console` sets per handle once its runner performs through the engine (BL-2318), and it
  needs no change to `ManagedCurlEngine`'s constructors or any public `Curl.Api` member. A future
  `curl_global_trace` in `Libcurl` can set it on each handle it creates. It defaults to
  `EngineTraceComponents.None`; `Duplicate` copies it and `Reset` keeps it, as `curl_easy_reset` does
  not touch a global setting.
- **`WithTraceComponentEvents` wraps the debug events in the runner's order:** `ResponseWaitTimerTraceEvents`
  innermost under `timer` (with the handle's `CURLOPT_TIMEOUT` and `CURLOPT_CONNECTTIMEOUT` as
  `TransferTimers`), then `ClientReaderResetTraceEvents` under `read`, and `ClientWriterTraceEvents`
  outermost under `write`, so `[WRITE] [OUT] done` comes before the `[READ]` line. The decorators sit
  inside `EngineConnectionIdTransferEvents`, as the runner's sit inside its connection-ID recording.
- **The `[TIMER]` lines are the ones the runner writes with the multi not traced** (`WaitLines(tracesExpiry: false)`).
  The engine has no multi of its own, and `MultiStateTraceEvents`, which writes the `[MULTI]` lines
  and the timers' `expires in` lines among them, stays in `Curl.Console`; placing those is BL-2318's.
- `--trace-config dns`'s `AsyncResolveTeardownTraceEvents` also stays in `Curl.Console` for now; it is
  not one of the three components BL-2363 names.

## Consequences

- A test performs through the easy handle and through the runner's own context with the same
  decorators and finds the debug callback's calls identical.
- BL-2318 sets `TraceComponents` from `EasyOptionsFromCommandLine.TraceComponentsOf` and must still
  bring the `[MULTI]` lines (and the timer lines' place among them) and the DNS teardown line to the engine.
