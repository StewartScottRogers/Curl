# ADR-0974: A warning reaches the debug callback as an info line

- Status: Accepted
- Date: 2026-10-28
- Decision: Decided by Claude under Stewart's delegation (BL-3996, BL-3986)

## Context

BL-3986 added `ITransferEvents.ReportWarning` with a default no-op, so a wrapper that does not implement it drops
the warning. Most engine wrappers wrap other events and only need to pass it on. `DebugCallbackTransferEvents`
is the end of a chain: it turns events into `CURLOPT_DEBUGFUNCTION` calls for a `Libcurl` application. libcurl
has no separate warning channel; what it reports while a transfer runs goes through `Curl_infof`, which reaches
the debug callback as `CURLINFO_TEXT` when `CURLOPT_VERBOSE` is set. The `Warning: ` prefix is the curl tool's,
added when it writes to stderr.

## Decision

- Every Curl.Engine wrapper that forwards every `ITransferEvents` member forwards `ReportWarning` to its inner
  events unchanged.
- `DebugCallbackTransferEvents.ReportWarning` hands the text to the callback as one `CurlInfoType.Text` line,
  ended with `\n`, without the `Warning: ` prefix, as `ReportInfo` does.

## Consequences

- A library's warning reaches the console's `WarningWritingTransferEvents` through the engine, and a `Libcurl`
  application with a verbose debug callback sees it as libcurl's own info text.
- `Curl.Engine.UnitTests/EngineTransferEventWarningTests.cs` pins both. The Curl.Networking and Curl.Output
  wrappers get the same forward under BL-4027.
