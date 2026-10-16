# ADR-0543 — The engine's trace events and progress reach the debug and xferinfo callbacks

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2360.

## Context

ADR-0541 and ADR-0542 left `ManagedEasyTransfer` building its context with no `ITransferEvents` and
no `ITransferProgress`, so a `Curl.Api` or `Libcurl` caller saw neither the verbose trace nor the
progress. To route `Curl.Console`'s serial transfers through `IEasyTransfer.PerformAsync` without
changing a byte of `-v`, `--trace` or `-#` output (BL-2318), every event the runner's events
receive and every report its progress sink receives must reach the handle's callbacks. libcurl's
`CURLOPT_DEBUGFUNCTION` carries only a `curl_infotype` and bytes; the runner's events also carry
structured facts (a connection opened, a TLS handshake), and its `--trace-ids` lines a transfer and
connection number.

## Decision

- **`DebugCallback` keeps libcurl's signature.** No member is added: a caller that wants more than
  an info type and bytes gets it as libcurl's callers do.
- **Every event is handed over as libcurl's `Curl_debug` hands it, only under `CURLOPT_VERBOSE`.**
  `DebugCallbackTransferEvents` gives an info line as `CURLINFO_TEXT` with its line feed; a
  connection opened or reused, a TLS handshake, trust or message as the `CURLINFO_TEXT` lines `-v`
  prints for it, worded by `Curl.Output`'s `TransferEventInfoText` for the running platform's curl
  build (ADR-0085) - `Curl.Output` makes its internals visible to `Curl.Engine.UnitLibrary` for
  this; request and response head bytes as `CURLINFO_HEADER_OUT` and `CURLINFO_HEADER_IN`; body
  bytes as `CURLINFO_DATA_OUT` and `CURLINFO_DATA_IN`; TLS record bytes as `CURLINFO_SSL_DATA_OUT`
  and `CURLINFO_SSL_DATA_IN` in the OpenSSL build only, since the Schannel build passes none. The
  callback's return value is ignored, as libcurl ignores it. With verbose off, or no debug callback,
  the context gets no events.
- **A component's name stays in the text.** curl's `Curl_trc_*` writes `[TIMER] `, `[READ] ` and
  the like at the start of the `CURLINFO_TEXT` line, so a handler's component line passes through
  unchanged. The decorators the runner adds from `--trace-config` (`ClientReaderResetTraceEvents`,
  `ClientWriterTraceEvents`, `ResponseWaitTimerTraceEvents`) need a trace setting the engine does
  not have yet: BL-2363.
- **The `[<xfer>-<conn>]` IDs are not in the bytes.** curl's tool reads `CURLINFO_XFER_ID` and
  `CURLINFO_CONN_ID` inside its debug callback; giving the handle live values during a perform is
  BL-2364.
- **Progress goes to the xferinfo callback, else the progress callback, unless `CURLOPT_NOPROGRESS`.**
  `CallbackTransferProgress` calls it on every report the handler makes with the download total,
  the bytes downloaded, the upload total and the bytes uploaded, an unknown total as 0, all four from
  zero when the transfer or a request within it starts. With `NoProgress` set (libcurl's default) or
  neither callback set, the context gets no progress sink.
- **A non-zero return aborts as libcurl's `pgrsupdate` does.** Any value but 0 and
  `CURL_PROGRESSFUNC_CONTINUE` cancels the attempt's token (linked to the caller's), and the perform
  returns `CurlCode.AbortedByCallback` with libcurl's `Callback aborted` text, whether the cancel
  stopped the attempt or it finished first; the callback is not called again. A caller's own cancel
  still ends the perform with `OperationCanceledException`.

## Consequences

- `Curl.Console` can build its `-v`, `--trace` and progress-meter output from the callbacks
  (BL-2318), once BL-2363 and BL-2364 add the component decorators and the IDs.
- The engine reaches `Curl.Output`'s internal wording; a change to that wording changes both the
  tool's `-v` lines and the debug callback's text, which is the intent.
